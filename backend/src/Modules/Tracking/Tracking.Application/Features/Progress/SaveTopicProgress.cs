using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Curriculum.Contracts;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Domain;
using Nirengi.Modules.Tracking.Domain.Progress;

namespace Nirengi.Modules.Tracking.Application.Features.Progress;

/// <param name="ChangeSeq">Position in the change feed; the sync cursor.</param>
public sealed record TopicProgressResponse(
    Guid TopicId,
    TopicProgressStatus Status,
    DateTimeOffset ClientModifiedAt,
    DateTimeOffset UpdatedAt,
    long ChangeSeq)
{
    public static TopicProgressResponse From(TopicProgress p) => new(p.TopicId, p.Status, p.ClientModifiedAt, p.UpdatedAt, p.ChangeSeq);
}

/// <summary>Sets the student's mark on a topic (idempotent PUT; an older edit than the stored one changes nothing).</summary>
public sealed record SaveTopicProgressCommand(Guid UserId, Guid TopicId, TopicProgressStatus? Status, DateTimeOffset? ClientModifiedAt)
    : ICommand<TopicProgressResponse>;

internal sealed class SaveTopicProgressValidator : AbstractValidator<SaveTopicProgressCommand>
{
    public SaveTopicProgressValidator()
    {
        RuleFor(c => c.TopicId).NotEmpty();
        RuleFor(c => c.Status).NotNull().IsInEnum();
        RuleFor(c => c.ClientModifiedAt).NotNull();
    }
}

internal sealed class SaveTopicProgressHandler(
    ITopicProgressRepository progress,
    ICurriculumCatalog catalog,
    ITrackingUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<SaveTopicProgressCommand, TopicProgressResponse>
{
    public async Task<Result<TopicProgressResponse>> Handle(SaveTopicProgressCommand command, CancellationToken cancellationToken)
    {
        var mark = await progress.Get(command.UserId, command.TopicId, cancellationToken);
        if (mark is null && (await catalog.GetTopics([command.TopicId], cancellationToken)).Count == 0)
        {
            return TrackingErrors.UnknownTopic;
        }

        var now = clock.UtcNow;
        if (mark is null)
        {
            mark = TopicProgress.Create(command.UserId, command.TopicId, command.Status!.Value, command.ClientModifiedAt!.Value, now);
            progress.Add(mark);
        }
        else if (!mark.Apply(command.Status!.Value, command.ClientModifiedAt!.Value, now))
        {
            return TopicProgressResponse.From(mark);
        }

        return await unitOfWork.TrySaveChanges(cancellationToken)
            ? TopicProgressResponse.From(mark)
            : TrackingErrors.TopicProgressConflict;
    }
}
