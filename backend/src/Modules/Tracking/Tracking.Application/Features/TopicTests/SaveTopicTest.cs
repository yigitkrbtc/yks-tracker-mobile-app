using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Curriculum.Contracts;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Contracts;
using Nirengi.Modules.Tracking.Domain;
using Nirengi.Modules.Tracking.Domain.TopicTests;

namespace Nirengi.Modules.Tracking.Application.Features.TopicTests;

public sealed record TopicResultInput(
    Guid? TopicId,
    int? Total,
    int? Correct,
    int? Wrong,
    int? Blank,
    int? MistakeKnowledge,
    int? MistakeAttention,
    int? MistakeTime);

/// <summary>
/// Creates or replaces a topic test by its client id (idempotent PUT, ADR-0005). An older edit than the stored
/// one changes nothing and gets the stored state back, so the client can reconcile.
/// </summary>
public sealed record SaveTopicTestCommand(
    Guid UserId,
    Guid Id,
    DateOnly? StudiedOn,
    Guid? SourceId,
    string? SourceName,
    bool? IsRepeat,
    Difficulty? Difficulty,
    int? DurationMinutes,
    string? Note,
    IReadOnlyList<TopicResultInput>? Results,
    DateTimeOffset? ClientModifiedAt) : ICommand<TopicTestResponse>;

internal sealed class SaveTopicTestValidator : AbstractValidator<SaveTopicTestCommand>
{
    public SaveTopicTestValidator(IClock clock)
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ClientModifiedAt).NotNull();
        RuleFor(c => c.IsRepeat).NotNull();
        // Separate rules: a trailing When() would also guard NotNull and let a missing value through.
        RuleFor(c => c.StudiedOn).NotNull();
        RuleFor(c => c.StudiedOn)
            .Must(d => TopicTestRules.IsAllowedStudyDate(d!.Value, clock.UtcNow)).When(c => c.StudiedOn.HasValue)
            .WithMessage($"Tarih bugün ile son {TopicTestRules.MaxDaysBack} gün arasında olmalı.");

        RuleFor(c => c.SourceName).MaximumLength(TopicTestRules.SourceNameMaxLength);
        RuleFor(c => c)
            .Must(c => c.SourceId.HasValue != !string.IsNullOrWhiteSpace(c.SourceName))
            .WithName("Source")
            .WithMessage("Listeden bir kaynak seç ya da kaynağın adını yaz (ikisi birden olmaz).");

        RuleFor(c => c.Difficulty).IsInEnum();
        RuleFor(c => c.DurationMinutes).InclusiveBetween(1, TopicTestRules.MaxDurationMinutes);
        RuleFor(c => c.Note).MaximumLength(TopicTestRules.NoteMaxLength);

        RuleFor(c => c.Results).NotNull();
        RuleFor(c => c.Results)
            .Must(r => r!.Count is >= 1 and <= TopicTestRules.MaxTopics)
            .WithMessage($"Bir testte 1 ile {TopicTestRules.MaxTopics} arasında konu olur.")
            .Must(r => r!.Where(x => x?.TopicId is not null).Select(x => x.TopicId).Distinct().Count() == r!.Count(x => x?.TopicId is not null))
            .WithMessage("Bir konu aynı testte iki kez olamaz.")
            .When(c => c.Results is not null);
        RuleForEach(c => c.Results).NotNull().SetValidator(new TopicResultInputValidator());
    }
}

internal sealed class TopicResultInputValidator : AbstractValidator<TopicResultInput>
{
    public TopicResultInputValidator()
    {
        RuleFor(r => r.TopicId).NotNull().NotEqual(Guid.Empty);
        RuleFor(r => r.Total).NotNull().InclusiveBetween(1, TopicTestRules.MaxQuestionsPerTopic);
        RuleFor(r => r.Correct).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(r => r.Wrong).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(r => r.Blank).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(r => r)
            .Must(r => r.Correct + r.Wrong + r.Blank == r.Total)
            .When(r => r.Total.HasValue && r.Correct.HasValue && r.Wrong.HasValue && r.Blank.HasValue)
            .WithName("Counts")
            .WithMessage("Doğru + yanlış + boş, soru sayısına eşit olmalı.");
        RuleFor(r => r.MistakeKnowledge).GreaterThanOrEqualTo(0);
        RuleFor(r => r.MistakeAttention).GreaterThanOrEqualTo(0);
        RuleFor(r => r.MistakeTime).GreaterThanOrEqualTo(0);
        RuleFor(r => r)
            .Must(r => (r.MistakeKnowledge ?? 0) + (r.MistakeAttention ?? 0) + (r.MistakeTime ?? 0) <= r.Wrong)
            .When(r => r.Wrong.HasValue)
            .WithName("MistakeReasons")
            .WithMessage("Yanlış nedenlerinin toplamı yanlış sayısını aşamaz.");
    }
}

internal sealed class SaveTopicTestHandler(
    ITopicTestRepository tests,
    ICurriculumCatalog catalog,
    ITrackingUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<SaveTopicTestCommand, TopicTestResponse>
{
    public async Task<Result<TopicTestResponse>> Handle(SaveTopicTestCommand command, CancellationToken cancellationToken)
    {
        var test = await tests.Get(command.Id, cancellationToken);
        if (test is not null && test.UserId != command.UserId)
        {
            return TrackingErrors.TopicTestNotFound;
        }

        var data = ToData(command);
        var error = await CheckReferences(data, test, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var now = clock.UtcNow;
        var before = test?.Results.Select(r => r.TopicId).ToList() ?? [];
        if (test is null)
        {
            test = TopicTest.Create(command.Id, command.UserId, data, command.ClientModifiedAt!.Value, now);
            tests.Add(test);
        }
        else if (!test.Apply(data, command.ClientModifiedAt!.Value, now))
        {
            return TopicTestResponse.From(test);
        }

        unitOfWork.Enqueue(new TopicTestChanged(
            Guid.CreateVersion7(now), now, command.UserId, test.Id, [.. before.Union(test.Results.Select(r => r.TopicId))]));

        // Two devices pushing the same id at once: the loser gets 409 and its retry lands as an ordinary edit.
        return await unitOfWork.TrySaveChanges(cancellationToken)
            ? TopicTestResponse.From(test)
            : TrackingErrors.TopicTestConflict;
    }

    /// <summary>New topics and a newly picked source must be active; ones the stored test already has may be retired.</summary>
    private async Task<Error?> CheckReferences(TopicTestData data, TopicTest? existing, CancellationToken cancellationToken)
    {
        var ids = data.Results.Select(r => r.TopicId).ToList();
        var known = (await catalog.GetTopics(ids, cancellationToken)).ToDictionary(t => t.Id);
        var kept = existing?.Results.Select(r => r.TopicId).ToHashSet() ?? [];
        if (ids.Any(id => !known.TryGetValue(id, out var topic) || (!topic.IsActive && !kept.Contains(id))))
        {
            return TrackingErrors.UnknownTopic;
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

    private static TopicTestData ToData(SaveTopicTestCommand c) => new(
        c.StudiedOn!.Value,
        c.SourceId,
        c.SourceId.HasValue ? null : c.SourceName,
        c.IsRepeat!.Value,
        c.Difficulty,
        c.DurationMinutes,
        c.Note,
        [.. c.Results!.Select(r => new TopicResultData(
            r.TopicId!.Value, r.Total!.Value, r.Correct!.Value, r.Wrong!.Value, r.Blank!.Value, r.MistakeKnowledge, r.MistakeAttention, r.MistakeTime))]);
}
