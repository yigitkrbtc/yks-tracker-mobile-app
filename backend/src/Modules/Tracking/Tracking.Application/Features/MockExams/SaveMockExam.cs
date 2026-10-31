using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Curriculum.Contracts;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Contracts;
using Nirengi.Modules.Tracking.Domain;
using Nirengi.Modules.Tracking.Domain.MockExams;
using Nirengi.Modules.Tracking.Domain.TopicTests;

namespace Nirengi.Modules.Tracking.Application.Features.MockExams;

/// <param name="QuestionCount">Optional for TYT/AYT (the template's count is used); a branch exam may send its own.</param>
public sealed record MockSectionInput(string? SectionCode, int? QuestionCount, int? Correct, int? Wrong);

public sealed record TopicMarkInput(
    string? SectionCode,
    Guid? TopicId,
    int? Wrong,
    int? Blank,
    int? MistakeKnowledge,
    int? MistakeAttention,
    int? MistakeTime);

/// <summary>
/// Creates or replaces a mock exam by its client id (idempotent PUT, ADR-0005), topic marks included. An older
/// edit than the stored one changes nothing and gets the stored state back, so the client can reconcile.
/// </summary>
public sealed record SaveMockExamCommand(
    Guid UserId,
    Guid Id,
    MockExamKind? Kind,
    DateOnly? TakenOn,
    Guid? SourceId,
    string? SourceName,
    string? Title,
    int? DurationMinutes,
    string? Note,
    IReadOnlyList<MockSectionInput>? Sections,
    IReadOnlyList<TopicMarkInput>? Marks,
    DateTimeOffset? ClientModifiedAt) : ICommand<MockExamResponse>;

internal sealed class SaveMockExamValidator : AbstractValidator<SaveMockExamCommand>
{
    public SaveMockExamValidator(IClock clock)
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ClientModifiedAt).NotNull();
        RuleFor(c => c.Kind).NotNull().IsInEnum();
        RuleFor(c => c.TakenOn).NotNull();
        RuleFor(c => c.TakenOn)
            .Must(d => TopicTestRules.IsAllowedStudyDate(d!.Value, clock.UtcNow)).When(c => c.TakenOn.HasValue)
            .WithMessage($"Tarih bugün ile son {TopicTestRules.MaxDaysBack} gün arasında olmalı.");

        RuleFor(c => c.SourceName).MaximumLength(MockExamRules.SourceNameMaxLength);
        RuleFor(c => c)
            .Must(c => c.SourceId.HasValue != !string.IsNullOrWhiteSpace(c.SourceName))
            .WithName("Source")
            .WithMessage("Listeden bir yayınevi seç ya da adını yaz (ikisi birden olmaz).");

        RuleFor(c => c.Title).MaximumLength(MockExamRules.TitleMaxLength);
        RuleFor(c => c.DurationMinutes).InclusiveBetween(1, MockExamRules.MaxDurationMinutes);
        RuleFor(c => c.Note).MaximumLength(MockExamRules.NoteMaxLength);

        RuleFor(c => c.Sections).NotNull();
        RuleFor(c => c.Sections)
            .Must(s => s!.Count is >= 1 and <= MockExamRules.MaxSections)
            .WithMessage("Denemede en az bir çözülmüş bölüm olmalı.")
            .Must(s => Distinct(s!.Where(x => x?.SectionCode is not null).Select(x => x.SectionCode!)))
            .WithMessage("Bir bölüm aynı denemede iki kez olamaz.")
            .When(c => c.Sections is not null);
        RuleForEach(c => c.Sections).NotNull().SetValidator(new MockSectionInputValidator());

        RuleFor(c => c.Marks).NotNull();
        RuleFor(c => c.Marks)
            .Must(m => m!.Count <= MockExamRules.MaxMarks)
            .WithMessage($"En fazla {MockExamRules.MaxMarks} konu etiketi olur.")
            .Must(m => Distinct(m!.Where(x => x?.SectionCode is not null && x.TopicId is not null).Select(x => $"{x.SectionCode}|{x.TopicId}")))
            .WithMessage("Bir konu aynı bölümde iki kez etiketlenemez.")
            .When(c => c.Marks is not null);
        RuleForEach(c => c.Marks).NotNull().SetValidator(new TopicMarkInputValidator());

        RuleFor(c => c)
            .Must(MarksFitSections)
            .When(c => c.Sections is not null && c.Marks is not null)
            .WithName("Marks")
            .WithMessage("Etiketlenen yanlış ve boşlar bölümün yanlış ve boş sayısını aşamaz.");
    }

    private static bool Distinct(IEnumerable<string> keys)
    {
        var list = keys.ToList();
        return list.Distinct(StringComparer.Ordinal).Count() == list.Count;
    }

    /// <summary>
    /// Every mark belongs to a sent section, and per section the marked wrong and blank stay within what was lost.
    /// Blank counts use the sent question count; a TYT/AYT count that differs from the template is refused later.
    /// </summary>
    private static bool MarksFitSections(SaveMockExamCommand c)
    {
        var sections = c.Sections!
            .Where(s => s?.SectionCode is not null)
            .GroupBy(s => s!.SectionCode!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var group in c.Marks!.Where(m => m?.SectionCode is not null).GroupBy(m => m!.SectionCode!, StringComparer.Ordinal))
        {
            if (!sections.TryGetValue(group.Key, out var s))
            {
                return false;
            }

            if (s.Correct is null || s.Wrong is null)
            {
                continue;
            }

            if (group.Sum(m => m!.Wrong ?? 0) > s.Wrong
                || (s.QuestionCount is { } q && group.Sum(m => m!.Blank ?? 0) > q - s.Correct - s.Wrong))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class MockSectionInputValidator : AbstractValidator<MockSectionInput>
{
    public MockSectionInputValidator()
    {
        RuleFor(s => s.SectionCode).NotEmpty();
        RuleFor(s => s.QuestionCount).InclusiveBetween(1, MockExamRules.MaxBranchQuestions);
        RuleFor(s => s.Correct).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(s => s.Wrong).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(s => s)
            .Must(s => s.Correct + s.Wrong <= s.QuestionCount)
            .When(s => s.QuestionCount.HasValue && s.Correct.HasValue && s.Wrong.HasValue)
            .WithName("Counts")
            .WithMessage("Doğru + yanlış, soru sayısını aşamaz.");
    }
}

internal sealed class TopicMarkInputValidator : AbstractValidator<TopicMarkInput>
{
    public TopicMarkInputValidator()
    {
        RuleFor(m => m.SectionCode).NotEmpty();
        RuleFor(m => m.TopicId).NotNull().NotEqual(Guid.Empty);
        RuleFor(m => m.Wrong).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(m => m.Blank).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(m => m)
            .Must(m => m.Wrong + m.Blank >= 1)
            .When(m => m.Wrong.HasValue && m.Blank.HasValue)
            .WithName("Counts")
            .WithMessage("Etiket en az bir yanlış ya da boş içermeli.");
        RuleFor(m => m.MistakeKnowledge).GreaterThanOrEqualTo(0);
        RuleFor(m => m.MistakeAttention).GreaterThanOrEqualTo(0);
        RuleFor(m => m.MistakeTime).GreaterThanOrEqualTo(0);
        RuleFor(m => m)
            .Must(m => (m.MistakeKnowledge ?? 0) + (m.MistakeAttention ?? 0) + (m.MistakeTime ?? 0) <= m.Wrong)
            .When(m => m.Wrong.HasValue)
            .WithName("MistakeReasons")
            .WithMessage("Yanlış nedenlerinin toplamı yanlış sayısını aşamaz.");
    }
}

internal sealed class SaveMockExamHandler(
    IMockExamRepository exams,
    ICurriculumCatalog catalog,
    ITrackingUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<SaveMockExamCommand, MockExamResponse>
{
    public async Task<Result<MockExamResponse>> Handle(SaveMockExamCommand command, CancellationToken cancellationToken)
    {
        var exam = await exams.Get(command.Id, cancellationToken);
        if (exam is not null && exam.UserId != command.UserId)
        {
            return TrackingErrors.MockExamNotFound;
        }

        var template = (await catalog.GetMockSections(cancellationToken)).ToDictionary(
            s => s.Code,
            s => new MockSectionDefinition(s.Code, s.TestCode, s.Session, s.SubjectCodes, s.QuestionCount, s.ParentCode, s.AlternativeOf),
            StringComparer.Ordinal);
        var unknownSection = command.Sections!.FirstOrDefault(s => !template.ContainsKey(s.SectionCode!));
        if (unknownSection is not null)
        {
            return TrackingErrors.UnknownMockSection;
        }

        var data = ToData(command, template);
        var error = await CheckReferences(data, exam, template, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var now = clock.UtcNow;
        var sectionsBefore = exam?.Sections.Select(s => s.SectionCode).ToList() ?? [];
        var topicsBefore = exam?.Marks.Select(m => m.TopicId).ToList() ?? [];
        if (exam is null)
        {
            exam = MockExam.Create(command.Id, command.UserId, data, command.ClientModifiedAt!.Value, now);
            exams.Add(exam);
        }
        else if (!exam.Apply(data, command.ClientModifiedAt!.Value, now))
        {
            return MockExamResponse.From(exam);
        }

        unitOfWork.Enqueue(new MockExamChanged(
            Guid.CreateVersion7(now),
            now,
            command.UserId,
            exam.Id,
            [.. sectionsBefore.Union(exam.Sections.Select(s => s.SectionCode), StringComparer.Ordinal)],
            [.. topicsBefore.Union(exam.Marks.Select(m => m.TopicId))]));

        // Two devices pushing the same id at once: the loser gets 409 and its retry lands as an ordinary edit.
        return await unitOfWork.TrySaveChanges(cancellationToken)
            ? MockExamResponse.From(exam)
            : TrackingErrors.MockExamConflict;
    }

    /// <summary>
    /// The template rules, then topics and source: newly marked topics and a newly picked source must be active;
    /// ones the stored exam already has may be retired.
    /// </summary>
    private async Task<Error?> CheckReferences(
        MockExamData data,
        MockExam? existing,
        Dictionary<string, MockSectionDefinition> template,
        CancellationToken cancellationToken)
    {
        var ids = data.Marks.Select(m => m.TopicId).Distinct().ToList();
        var known = (await catalog.GetTopics(ids, cancellationToken)).ToDictionary(t => t.Id);
        var kept = existing?.Marks.Select(m => m.TopicId).ToHashSet() ?? [];
        if (ids.Any(id => known.TryGetValue(id, out var topic) && !topic.IsActive && !kept.Contains(id)))
        {
            return TrackingErrors.UnknownTopic;
        }

        var error = MockExamTemplate.Check(data, template, known.ToDictionary(k => k.Key, k => k.Value.SubjectCode));
        if (error is not null)
        {
            return error;
        }

        if (data.SourceId is { } sourceId && sourceId != existing?.SourceId)
        {
            var source = await catalog.GetSource(sourceId, cancellationToken);
            if (source is not { IsActive: true })
            {
                return TrackingErrors.UnknownSource;
            }
        }

        return null;
    }

    private static MockExamData ToData(SaveMockExamCommand c, Dictionary<string, MockSectionDefinition> template) => new(
        c.Kind!.Value,
        c.TakenOn!.Value,
        c.SourceId,
        c.SourceId.HasValue ? null : c.SourceName,
        c.Title,
        c.DurationMinutes,
        c.Note,
        [.. c.Sections!.Select(s => new MockSectionData(
            s.SectionCode!, s.QuestionCount ?? template[s.SectionCode!].QuestionCount, s.Correct!.Value, s.Wrong!.Value))],
        [.. c.Marks!.Select(m => new TopicMarkData(
            m.SectionCode!, m.TopicId!.Value, m.Wrong!.Value, m.Blank!.Value, m.MistakeKnowledge, m.MistakeAttention, m.MistakeTime))]);
}
