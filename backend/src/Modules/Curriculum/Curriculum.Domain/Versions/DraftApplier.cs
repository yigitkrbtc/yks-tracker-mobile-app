using System.Globalization;
using System.Text.RegularExpressions;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Curriculum.Domain.Calendar;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;

namespace Nirengi.Modules.Curriculum.Domain.Versions;

/// <param name="Operations">The draft with every added topic's code filled in.</param>
/// <param name="Lineage">Merge and split links the draft creates.</param>
public sealed record AppliedDraft(CurriculumContent Content, IReadOnlyList<CurriculumOperation> Operations, IReadOnlyList<LineageLink> Lineage);

/// <summary>
/// Applies a draft to the live content without I/O (ADR-0019). Every rule of the seed parser holds for admin
/// edits too: codes are permanent and never reused, grades 9–12, a subject keeps at least one active topic.
/// </summary>
public static partial class DraftApplier
{
    public const int MaxOperations = 500;

    public const int MaxSubtopics = 30;

    public const int SubtopicMaxLength = 200;

    /// <param name="subjectCodes">Subjects of the exam structure.</param>
    /// <param name="knownTopicCodes">Every code ever used, retired topics included; a new code never repeats one.</param>
    public static Result<AppliedDraft> Apply(
        CurriculumContent live,
        IReadOnlySet<string> subjectCodes,
        IReadOnlySet<string> knownTopicCodes,
        IReadOnlyList<CurriculumOperation> operations)
    {
        if (operations.Count > MaxOperations)
        {
            return Invalid($"Bir taslakta en çok {MaxOperations} işlem olabilir.");
        }

        var topics = live.Topics.ToDictionary(t => t.Code, StringComparer.Ordinal);
        var order = live.Topics.Select(t => t.Code).ToList();
        var sources = live.Sources.ToDictionary(s => s.Code, StringComparer.Ordinal);
        var sourceOrder = live.Sources.Select(s => s.Code).ToList();
        var calendars = live.Calendars.ToDictionary(c => c.ExamYear);
        var used = new HashSet<string>(knownTopicCodes, StringComparer.Ordinal);
        used.UnionWith(topics.Keys);
        var lineage = new List<LineageLink>();
        var normalized = new List<CurriculumOperation>(operations.Count);

        for (var i = 0; i < operations.Count; i++)
        {
            var at = $"{i + 1}. işlem";
            var op = operations[i];
            string? problem = null;
            switch (op)
            {
                case AddTopic add:
                    if (!subjectCodes.Contains(add.SubjectCode))
                    {
                        problem = $"bilinmeyen ders {add.SubjectCode}.";
                        break;
                    }

                    var code = string.IsNullOrWhiteSpace(add.Code) ? NextCode(add.SubjectCode, used) : add.Code.Trim();
                    if (!code.StartsWith(add.SubjectCode + ".", StringComparison.Ordinal) || !CodePattern().IsMatch(code) || code.Length > Topic.CodeMaxLength)
                    {
                        problem = $"konu kodu {code} '{add.SubjectCode}.NN' biçiminde olmalı.";
                        break;
                    }

                    if (used.Contains(code))
                    {
                        problem = $"konu kodu {code} daha önce kullanıldı; kodlar tekrar kullanılmaz.";
                        break;
                    }

                    problem = TopicFieldsProblem(add.Name, add.Subtopics, add.GradeMin, add.GradeMax, add.Description)
                        ?? (add.SortOrder is < 0 ? "sıra negatif olamaz." : null);
                    if (problem is null)
                    {
                        used.Add(code);
                        var sort = add.SortOrder ?? topics.Values.Where(t => t.SubjectCode == add.SubjectCode).Select(t => t.SortOrder).DefaultIfEmpty(-1).Max() + 1;
                        topics[code] = new TopicState(code, add.SubjectCode, add.Name.Trim(), Clean(add.Subtopics), add.GradeMin, add.GradeMax, Trim(add.Description), sort, true);
                        order.Add(code);
                        op = add with { Code = code };
                    }

                    break;

                case UpdateTopic update:
                    if (!topics.TryGetValue(update.Code, out var current))
                    {
                        problem = $"konu {update.Code} bulunamadı.";
                        break;
                    }

                    problem = TopicFieldsProblem(update.Name, update.Subtopics, update.GradeMin, update.GradeMax, update.Description)
                        ?? (update.SortOrder is < 0 ? "sıra negatif olamaz." : null);
                    if (problem is null)
                    {
                        topics[update.Code] = current with
                        {
                            Name = update.Name.Trim(),
                            Subtopics = Clean(update.Subtopics),
                            GradeMin = update.GradeMin,
                            GradeMax = update.GradeMax,
                            Description = Trim(update.Description),
                            SortOrder = update.SortOrder ?? current.SortOrder,
                        };
                    }

                    break;

                case RetireTopic retire:
                    problem = SetActive(topics, retire.Code, active: false);
                    break;

                case ReactivateTopic reactivate:
                    problem = SetActive(topics, reactivate.Code, active: true);
                    break;

                case MergeTopics merge:
                    problem = LinkProblem(topics, merge.FromCodes, [merge.ToCode], minFrom: 1);
                    if (problem is null)
                    {
                        foreach (var from in merge.FromCodes)
                        {
                            topics[from] = topics[from] with { IsActive = false };
                            lineage.Add(new LineageLink(from, merge.ToCode, LineageRelation.Merged));
                        }
                    }

                    break;

                case SplitTopic split:
                    problem = split.ToCodes.Count < 2 ? "bölme en az iki yeni konu ister." : LinkProblem(topics, [split.FromCode], split.ToCodes, minFrom: 1);
                    if (problem is null)
                    {
                        topics[split.FromCode] = topics[split.FromCode] with { IsActive = false };
                        lineage.AddRange(split.ToCodes.Select(to => new LineageLink(split.FromCode, to, LineageRelation.Split)));
                    }

                    break;

                case AddSource addSource:
                    problem = SourceProblem(addSource.Code, addSource.Name, addSource.Kind)
                        ?? (sources.ContainsKey(addSource.Code) ? $"kaynak kodu {addSource.Code} zaten var." : null);
                    if (problem is null)
                    {
                        sources[addSource.Code] = new SourceState(addSource.Code, addSource.Name.Trim(), addSource.Kind, sources.Count == 0 ? 0 : sources.Values.Max(s => s.SortOrder) + 1, true);
                        sourceOrder.Add(addSource.Code);
                    }

                    break;

                case UpdateSource updateSource:
                    problem = !sources.TryGetValue(updateSource.Code, out var source)
                        ? $"kaynak {updateSource.Code} bulunamadı."
                        : SourceProblem(updateSource.Code, updateSource.Name, updateSource.Kind);
                    if (problem is null)
                    {
                        sources[updateSource.Code] = sources[updateSource.Code] with { Name = updateSource.Name.Trim(), Kind = updateSource.Kind };
                    }

                    break;

                case RetireSource retireSource:
                    problem = SetSourceActive(sources, retireSource.Code, active: false);
                    break;

                case ReactivateSource reactivateSource:
                    problem = SetSourceActive(sources, reactivateSource.Code, active: true);
                    break;

                case SetCalendar set:
                    problem = set.Calendar is null ? "takvim boş." : set.Calendar.Problem();
                    if (problem is null)
                    {
                        calendars[set.Calendar!.ExamYear] = set.Calendar;
                    }

                    break;

                default:
                    problem = "bilinmeyen işlem türü.";
                    break;
            }

            if (problem is not null)
            {
                return Invalid($"{at}: {problem}");
            }

            normalized.Add(op);
        }

        var content = new CurriculumContent(
            [.. order.Select(c => topics[c])],
            [.. sourceOrder.Select(c => sources[c])],
            [.. calendars.Values.OrderBy(c => c.ExamYear)]);

        var empty = subjectCodes.Order(StringComparer.Ordinal)
            .FirstOrDefault(s => live.Topics.Any(t => t.SubjectCode == s && t.IsActive) && !content.Topics.Any(t => t.SubjectCode == s && t.IsActive));
        if (empty is not null)
        {
            return Invalid($"{empty} dersinde aktif konu kalmıyor.");
        }

        return new AppliedDraft(content, normalized, lineage);
    }

    /// <summary><c>AYT.AMAT</c> → <c>AYT.AMAT.43</c>: one more than the highest number ever used in the subject.</summary>
    public static string NextCode(string subjectCode, IReadOnlySet<string> used)
    {
        var prefix = subjectCode + ".";
        var max = used
            .Where(c => c.StartsWith(prefix, StringComparison.Ordinal))
            .Select(c => int.TryParse(c.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return prefix + (max + 1).ToString("00", CultureInfo.InvariantCulture);
    }

    private static string? TopicFieldsProblem(string name, IReadOnlyList<string>? subtopics, int gradeMin, int gradeMax, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > Topic.NameMaxLength)
        {
            return $"konu adı boş olamaz ve en çok {Topic.NameMaxLength} karakter olabilir.";
        }

        if (subtopics is null || subtopics.Count == 0 || subtopics.Count > MaxSubtopics
            || subtopics.Any(s => string.IsNullOrWhiteSpace(s) || s.Trim().Length > SubtopicMaxLength || s.Contains(';', StringComparison.Ordinal)))
        {
            return $"1–{MaxSubtopics} alt konu gerekli; her biri dolu, en çok {SubtopicMaxLength} karakter ve ';' içermez.";
        }

        if (gradeMin is < 9 or > 12 || gradeMax < gradeMin || gradeMax > 12)
        {
            return "sınıf aralığı 9–12 içinde olmalı.";
        }

        return description is { Length: > Topic.DescriptionMaxLength } ? $"açıklama en çok {Topic.DescriptionMaxLength} karakter olabilir." : null;
    }

    private static string? SourceProblem(string code, string name, SourceKind kind)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > Source.CodeMaxLength || !SourceCodePattern().IsMatch(code))
        {
            return $"kaynak kodu büyük harf, rakam ve '_' ile en çok {Source.CodeMaxLength} karakter olmalı.";
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > Source.NameMaxLength)
        {
            return $"kaynak adı boş olamaz ve en çok {Source.NameMaxLength} karakter olabilir.";
        }

        return Enum.IsDefined(kind) ? null : "bilinmeyen kaynak türü.";
    }

    private static string? SetActive(Dictionary<string, TopicState> topics, string code, bool active)
    {
        if (!topics.TryGetValue(code, out var topic))
        {
            return $"konu {code} bulunamadı.";
        }

        if (topic.IsActive == active)
        {
            return active ? $"konu {code} zaten aktif." : $"konu {code} zaten emekli.";
        }

        topics[code] = topic with { IsActive = active };
        return null;
    }

    private static string? SetSourceActive(Dictionary<string, SourceState> sources, string code, bool active)
    {
        if (!sources.TryGetValue(code, out var source))
        {
            return $"kaynak {code} bulunamadı.";
        }

        if (source.IsActive == active)
        {
            return active ? $"kaynak {code} zaten aktif." : $"kaynak {code} zaten emekli.";
        }

        sources[code] = source with { IsActive = active };
        return null;
    }

    /// <summary>Every code exists and is active, no code is on both sides, all topics share one subject.</summary>
    private static string? LinkProblem(Dictionary<string, TopicState> topics, IReadOnlyList<string>? from, IReadOnlyList<string>? to, int minFrom)
    {
        if (from is null || to is null || from.Count < minFrom || to.Count == 0)
        {
            return "kaynak ve hedef konular gerekli.";
        }

        var all = from.Concat(to).ToList();
        if (all.Distinct(StringComparer.Ordinal).Count() != all.Count)
        {
            return "bir konu iki kez veya iki tarafta birden yer alamaz.";
        }

        var missing = all.FirstOrDefault(c => !topics.TryGetValue(c, out var t) || !t.IsActive);
        if (missing is not null)
        {
            return $"konu {missing} bulunamadı veya emekli.";
        }

        return all.Select(c => topics[c].SubjectCode).Distinct(StringComparer.Ordinal).Count() == 1
            ? null
            : "birleştirilen veya bölünen konular aynı dersten olmalı.";
    }

    private static List<string> Clean(IReadOnlyList<string> subtopics) => [.. subtopics.Select(s => s.Trim())];

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Error Invalid(string message) => CurriculumErrors.InvalidDraft(message);

    [GeneratedRegex(@"^(TYT|AYT)\.[A-Z]+\.\d{2,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[A-Z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceCodePattern();
}
