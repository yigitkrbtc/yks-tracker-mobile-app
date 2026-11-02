using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Curriculum.Application.Abstractions;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;

namespace Nirengi.Modules.Curriculum.Application.Features;

/// <summary>The whole published curriculum in one tree; clients filter it by the student's score types.</summary>
public sealed record GetCurriculumQuery : IQuery<CurriculumResponse>;

/// <summary>Only the content version, so an unchanged curriculum can be answered with 304.</summary>
public sealed record GetCurriculumVersionQuery : IQuery<string>;

/// <param name="Version">Content hash of the published curriculum; changes on every publish (ADR-0019).</param>
public sealed record CurriculumResponse(
    string Version,
    int ExamYear,
    IReadOnlyList<ScoreTypeDto> ScoreTypes,
    IReadOnlyList<ExamTestDto> Tests,
    IReadOnlyList<SubjectDto> Subjects,
    IReadOnlyList<SourceDto> Sources,
    IReadOnlyList<MockSectionDto> MockSections);

/// <param name="TestCodes">Tests a student of this score type studies; TYT tests are listed for every type.</param>
public sealed record ScoreTypeDto(string Code, string Name, IReadOnlyList<string> TestCodes);

/// <summary>A question source (publisher, past papers) a student picks when recording a test.</summary>
public sealed record SourceDto(Guid Id, string Code, string Name, SourceKind Kind);

/// <summary>A line of a mock exam's score sheet. A block (no parent, has children) is entered either as its total or by
/// its children, never both; an alternative (philosophy instead of religion) replaces <paramref name="AlternativeOf"/>.</summary>
/// <param name="SubjectCode">Null when the section spans every subject of its test.</param>
public sealed record MockSectionDto(
    string Code,
    string TestCode,
    string? SubjectCode,
    string Name,
    int QuestionCount,
    string? ParentCode,
    string? AlternativeOf);

public sealed record ExamTestDto(string Code, ExamSession Session, string Name, int QuestionCount);

public sealed record SubjectDto(string Code, ExamSession Session, string Name, IReadOnlyList<string> TestCodes, IReadOnlyList<TopicDto> Topics);

/// <param name="ExpectedQuestions">Sum over all tests of the topic. Kept for app versions before ADR-0019; new clients
/// sum <paramref name="Weights"/> over the tests of the student's score type.</param>
/// <param name="Tier">Highest tier over all tests (pre ADR-0019 field).</param>
/// <param name="Weights">Per test; one entry except AYT history and geography.</param>
public sealed record TopicDto(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyList<string> Subtopics,
    int GradeMin,
    int GradeMax,
    decimal ExpectedQuestions,
    WeightTier Tier,
    IReadOnlyList<TopicWeightDto> Weights);

/// <param name="ExpectedQuestions">Average questions per exam in this test (2022–2026), not an official ÖSYM figure.</param>
public sealed record TopicWeightDto(string TestCode, decimal ExpectedQuestions, WeightTier Tier);

internal sealed class GetCurriculumHandler(ICurriculumRepository curriculum) : IQueryHandler<GetCurriculumQuery, CurriculumResponse>
{
    public async Task<Result<CurriculumResponse>> Handle(GetCurriculumQuery query, CancellationToken cancellationToken)
    {
        var release = await curriculum.GetLatestRelease(cancellationToken);
        if (release is null)
        {
            return CurriculumErrors.NotSeeded;
        }

        var scoreTypes = await curriculum.GetScoreTypes(cancellationToken);
        var tests = await curriculum.GetTests(cancellationToken);
        var subjects = await curriculum.GetSubjects(cancellationToken);
        var topics = await curriculum.GetActiveTopics(cancellationToken);
        var sources = await curriculum.GetActiveSources(cancellationToken);
        var mockSections = await curriculum.GetMockSections(cancellationToken);
        var weights = (await curriculum.GetWeights(release.ExamYear, cancellationToken)).ToLookup(w => w.TopicId);
        var testOrder = tests.Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var topicsBySubject = topics.ToLookup(t => t.SubjectCode);

        return new CurriculumResponse(
            release.ContentHash,
            release.ExamYear,
            [.. scoreTypes.Select(s => new ScoreTypeDto(s.Id, s.Name, s.TestCodes))],
            [.. tests.Select(t => new ExamTestDto(t.Id, t.Session, t.Name, t.QuestionCount))],
            [.. subjects.Select(s => new SubjectDto(
                s.Id,
                s.Session,
                s.Name,
                s.TestCodes,
                [.. topicsBySubject[s.Id].Select(t => ToDto(t, [.. weights[t.Id].OrderBy(w => testOrder.GetValueOrDefault(w.TestCode))]))]))],
            [.. sources.Select(s => new SourceDto(s.Id, s.Code, s.Name, s.Kind))],
            [.. mockSections.Select(s => new MockSectionDto(s.Id, s.TestCode, s.SubjectCode, s.Name, s.QuestionCount, s.ParentCode, s.AlternativeOf))]);
    }

    private static TopicDto ToDto(Topic topic, List<TopicWeight> weights) => new(
        topic.Id,
        topic.Code,
        topic.Name,
        topic.Subtopics,
        topic.GradeMin,
        topic.GradeMax,
        weights.Sum(w => w.ExpectedQuestions),
        weights.Count == 0 ? WeightTier.Rare : weights.Max(w => w.Tier),
        [.. weights.Select(w => new TopicWeightDto(w.TestCode, w.ExpectedQuestions, w.Tier))]);
}

internal sealed class GetCurriculumVersionHandler(ICurriculumRepository curriculum) : IQueryHandler<GetCurriculumVersionQuery, string>
{
    public async Task<Result<string>> Handle(GetCurriculumVersionQuery query, CancellationToken cancellationToken)
    {
        var release = await curriculum.GetLatestRelease(cancellationToken);
        return release is null ? CurriculumErrors.NotSeeded : release.ContentHash;
    }
}
