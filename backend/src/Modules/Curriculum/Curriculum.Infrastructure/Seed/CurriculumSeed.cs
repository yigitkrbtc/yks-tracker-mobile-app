using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;

namespace Nirengi.Modules.Curriculum.Infrastructure.Seed;

internal sealed record TestSeed(string Code, ExamSession Session, string Name, int QuestionCount);

internal sealed record SubjectSeed(string Code, ExamSession Session, string Name, IReadOnlyList<string> TestCodes);

internal sealed record ScoreTypeSeed(string Code, string Name, IReadOnlyList<string> TestCodes);

internal sealed record TopicSeed(
    string Code,
    string SubjectCode,
    string Name,
    IReadOnlyList<string> Subtopics,
    int GradeMin,
    int GradeMax,
    string? Description,
    decimal ExpectedQuestions,
    WeightTier Tier,
    int YearsAsked,
    int YearsCounted);

internal sealed record SourceSeed(string Code, string Name, SourceKind Kind);

internal sealed record MockSectionSeed(
    string Code,
    string TestCode,
    string? SubjectCode,
    string Name,
    int QuestionCount,
    string? ParentCode,
    string? AlternativeOf);

/// <summary>
/// The curriculum as shipped in data/seed (compiled into this assembly). Parsing validates cross references,
/// so a broken seed fails the build's tests and the deploy, never a student's request.
/// </summary>
internal sealed record CurriculumSeed(
    int ExamYear,
    IReadOnlyList<TestSeed> Tests,
    IReadOnlyList<SubjectSeed> Subjects,
    IReadOnlyList<ScoreTypeSeed> ScoreTypes,
    IReadOnlyList<TopicSeed> Topics,
    IReadOnlyList<SourceSeed> Sources,
    IReadOnlyList<MockSectionSeed> MockSections,
    string Hash)
{
    // Bump when the mapping from files to rows changes, so already seeded databases pick it up.
    private const string ParserVersion = "3";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static CurriculumSeed LoadEmbedded()
    {
        var assembly = typeof(CurriculumSeed).Assembly;
        return Parse(
            ReadResource(assembly, "Seed.exam_structure.json"),
            ReadResource(assembly, "Seed.konu_taksonomisi.csv"),
            ReadResource(assembly, "Seed.konu_agirliklari.csv"),
            ReadResource(assembly, "Seed.kaynaklar.csv"));
    }

    public static CurriculumSeed Parse(string structureJson, string taxonomyCsv, string weightsCsv, string sourcesCsv)
    {
        var structure = JsonSerializer.Deserialize<StructureFile>(structureJson, Json)
            ?? throw new FormatException("exam_structure.json is empty.");

        var tests = structure.Tests.Select(t => new TestSeed(t.Code, ParseSession(t.Session), t.Name, t.QuestionCount)).ToList();
        var testCodes = tests.Select(t => t.Code).ToHashSet(StringComparer.Ordinal);

        var subjects = structure.Units.Select(u => new SubjectSeed(u.Code, ParseSession(u.Code.Split('.')[0]), u.Name, u.Tests)).ToList();
        foreach (var subject in subjects)
        {
            RequireKnown(subject.TestCodes, testCodes, $"subject {subject.Code}");
        }

        var scoreTypes = structure.ScoreTypes.Select(s => new ScoreTypeSeed(s.Code, s.Name, s.Tests)).ToList();
        foreach (var scoreType in scoreTypes)
        {
            RequireKnown(scoreType.TestCodes, testCodes, $"score type {scoreType.Code}");
        }

        var subjectCodes = subjects.Select(s => s.Code).ToHashSet(StringComparer.Ordinal);
        var mockSections = ParseMockSections(structure.MockSections ?? [], tests, subjects);
        var weights = Csv.Read(weightsCsv).ToDictionary(r => r["topic_code"], StringComparer.Ordinal);
        var topics = new List<TopicSeed>();
        foreach (var row in Csv.Read(taxonomyCsv))
        {
            var code = row["topic_code"];
            var subjectCode = $"{row["session"]}.{row["unit"]}";
            if (!subjectCodes.Contains(subjectCode))
            {
                throw new FormatException($"Topic {code} belongs to unknown subject {subjectCode}.");
            }

            if (!weights.TryGetValue(code, out var weight))
            {
                throw new FormatException($"Topic {code} has no row in konu_agirliklari.csv.");
            }

            var (gradeMin, gradeMax) = ParseGrades(row["grade"], code);
            var (yearsAsked, yearsCounted) = ParseYears(weight["years_present"], code);
            topics.Add(new TopicSeed(
                code,
                subjectCode,
                row["topic"].Trim(),
                [.. row["subtopics"].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                gradeMin,
                gradeMax,
                string.IsNullOrWhiteSpace(row["description"]) ? null : row["description"].Trim(),
                decimal.Parse(weight["q_i"], NumberStyles.Number, CultureInfo.InvariantCulture),
                ParseTier(weight["tier"], code),
                yearsAsked,
                yearsCounted));
        }

        var duplicate = topics.GroupBy(t => t.Code, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new FormatException($"Topic {duplicate.Key} appears more than once.");
        }

        var sources = Csv.Read(sourcesCsv)
            .Select(r => new SourceSeed(r["source_code"].Trim(), r["name"].Trim(), ParseSourceKind(r["kind"], r["source_code"])))
            .ToList();
        var badSource = sources.FirstOrDefault(s => s.Code.Length is 0 or > Source.CodeMaxLength || s.Name.Length is 0 or > Source.NameMaxLength);
        if (badSource is not null)
        {
            throw new FormatException($"Source '{badSource.Code}' has an empty or too long code or name.");
        }

        var duplicateSource = sources.GroupBy(s => s.Code, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicateSource is not null)
        {
            throw new FormatException($"Source {duplicateSource.Key} appears more than once.");
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join('\u001f', ParserVersion, structureJson, taxonomyCsv, weightsCsv, sourcesCsv))));
        return new CurriculumSeed(structure.ExamYear, tests, subjects, scoreTypes, topics, sources, mockSections, hash);
    }

    /// <summary>
    /// Checks the score sheet template: each listed test has one root section with the test's question count;
    /// children of a block add up to it; an alternative replaces a sibling of the same size.
    /// </summary>
    private static List<MockSectionSeed> ParseMockSections(List<MockSectionFile> files, List<TestSeed> tests, List<SubjectSeed> subjects)
    {
        var sections = files.Select(f => new MockSectionSeed(
                f.Code.Trim(),
                f.Test,
                string.IsNullOrWhiteSpace(f.Subject) ? null : f.Subject,
                f.Name.Trim(),
                f.QuestionCount,
                string.IsNullOrWhiteSpace(f.Parent) ? null : f.Parent,
                string.IsNullOrWhiteSpace(f.AlternativeOf) ? null : f.AlternativeOf))
            .ToList();
        if (sections.Count == 0)
        {
            return sections;
        }

        var byCode = new Dictionary<string, MockSectionSeed>(StringComparer.Ordinal);
        foreach (var s in sections)
        {
            if (s.Code.Length is 0 or > MockSection.CodeMaxLength || s.Name.Length is 0 or > MockSection.NameMaxLength || s.QuestionCount <= 0)
            {
                throw new FormatException($"Mock section '{s.Code}' has an empty or too long code or name, or no questions.");
            }

            if (!byCode.TryAdd(s.Code, s))
            {
                throw new FormatException($"Mock section {s.Code} appears more than once.");
            }
        }

        var testsByCode = tests.ToDictionary(t => t.Code, StringComparer.Ordinal);
        var subjectsByCode = subjects.ToDictionary(s => s.Code, StringComparer.Ordinal);
        foreach (var s in sections)
        {
            if (!testsByCode.ContainsKey(s.TestCode))
            {
                throw new FormatException($"Mock section {s.Code} refers to unknown test {s.TestCode}.");
            }

            if (s.SubjectCode is not null
                && (!subjectsByCode.TryGetValue(s.SubjectCode, out var subject) || !subject.TestCodes.Contains(s.TestCode)))
            {
                throw new FormatException($"Mock section {s.Code} refers to subject {s.SubjectCode}, which is not part of test {s.TestCode}.");
            }

            if (s.ParentCode is not null
                && (!byCode.TryGetValue(s.ParentCode, out var parent) || parent.ParentCode is not null || parent.TestCode != s.TestCode))
            {
                throw new FormatException($"Mock section {s.Code} has an invalid parent {s.ParentCode}.");
            }

            if (s.AlternativeOf is not null
                && (!byCode.TryGetValue(s.AlternativeOf, out var original)
                    || original.ParentCode != s.ParentCode
                    || original.AlternativeOf is not null
                    || original.QuestionCount != s.QuestionCount))
            {
                throw new FormatException($"Mock section {s.Code} is an alternative of {s.AlternativeOf}, which is not a same-size sibling.");
            }
        }

        foreach (var test in tests)
        {
            var roots = sections.Where(s => s.TestCode == test.Code && s.ParentCode is null).ToList();
            if (roots.Count != 1 || roots[0].QuestionCount != test.QuestionCount)
            {
                throw new FormatException($"Test {test.Code} needs exactly one root mock section with {test.QuestionCount} questions.");
            }

            var children = sections.Where(s => s.ParentCode == roots[0].Code && s.AlternativeOf is null).ToList();
            if (children.Count > 0 && children.Sum(c => c.QuestionCount) != test.QuestionCount)
            {
                throw new FormatException($"The mock sections of test {test.Code} do not add up to {test.QuestionCount} questions.");
            }
        }

        return sections;
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded seed file {name} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static ExamSession ParseSession(string value) =>
        Enum.TryParse<ExamSession>(value, ignoreCase: false, out var session) && Enum.IsDefined(session)
            ? session
            : throw new FormatException($"Unknown exam session '{value}'.");

    private static void RequireKnown(IEnumerable<string> codes, HashSet<string> known, string owner)
    {
        var unknown = codes.FirstOrDefault(c => !known.Contains(c));
        if (unknown is not null)
        {
            throw new FormatException($"The {owner} refers to unknown test {unknown}.");
        }
    }

    /// <summary>"11" or a range such as "9-12".</summary>
    private static (int Min, int Max) ParseGrades(string value, string code)
    {
        var parts = value.Split('-');
        if (parts.Length is < 1 or > 2
            || !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var min)
            || !int.TryParse(parts[^1], CultureInfo.InvariantCulture, out var max))
        {
            throw new FormatException($"Topic {code} has an invalid grade '{value}'.");
        }

        return (min, max);
    }

    /// <summary>"6/8": asked in 6 of the 8 years counted.</summary>
    private static (int Asked, int Counted) ParseYears(string value, string code)
    {
        var parts = value.Split('/');
        if (parts.Length != 2
            || !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var asked)
            || !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var counted))
        {
            throw new FormatException($"Topic {code} has an invalid years_present '{value}'.");
        }

        return (asked, counted);
    }

    private static WeightTier ParseTier(string value, string code) => value switch
    {
        "çok" => WeightTier.High,
        "orta" => WeightTier.Medium,
        "az" => WeightTier.Low,
        "nadir" => WeightTier.Rare,
        _ => throw new FormatException($"Topic {code} has an unknown tier '{value}'."),
    };

    private static SourceKind ParseSourceKind(string value, string code) => value.Trim() switch
    {
        "publisher" => SourceKind.Publisher,
        "official" => SourceKind.Official,
        "institution" => SourceKind.Institution,
        _ => throw new FormatException($"Source {code} has an unknown kind '{value}'."),
    };

    private sealed record StructureFile(
        int ExamYear,
        List<TestFile> Tests,
        List<UnitFile> Units,
        List<ScoreTypeFile> ScoreTypes,
        List<MockSectionFile>? MockSections);

    private sealed record TestFile(string Code, string Session, string Name, int QuestionCount);

    private sealed record UnitFile(string Code, string Name, List<string> Tests);

    private sealed record ScoreTypeFile(string Code, string Name, List<string> Tests);

    private sealed record MockSectionFile(
        string Code,
        string Test,
        string? Subject,
        string Name,
        int QuestionCount,
        string? Parent,
        string? AlternativeOf);
}
