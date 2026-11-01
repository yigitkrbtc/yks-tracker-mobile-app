using FluentValidation;
using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Contracts;
using Nirengi.Modules.Recommendation.Application.Abstractions;
using Nirengi.Modules.Recommendation.Domain.Plans;

namespace Nirengi.Modules.Recommendation.Application.Features;

public sealed record GetWeekPlanQuery(Guid UserId) : IQuery<WeekPlanResponse>;

/// <summary>Drops an item from this week's plan; the next candidate takes its slot. Dismissing twice is a no-op.</summary>
public sealed record DismissPlanItemCommand(Guid UserId, string ItemKey, DismissReason? Reason) : ICommand<WeekPlanResponse>;

internal sealed class DismissPlanItemValidator : AbstractValidator<DismissPlanItemCommand>
{
    public DismissPlanItemValidator()
    {
        RuleFor(c => c.ItemKey).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Reason).NotNull().IsInEnum();
    }
}

internal sealed class GetWeekPlanHandler(WeekPlanService plans) : IQueryHandler<GetWeekPlanQuery, WeekPlanResponse>
{
    public Task<Result<WeekPlanResponse>> Handle(GetWeekPlanQuery query, CancellationToken cancellationToken) =>
        plans.Get(query.UserId, cancellationToken);
}

internal sealed class DismissPlanItemHandler(WeekPlanService plans) : ICommandHandler<DismissPlanItemCommand, WeekPlanResponse>
{
    public Task<Result<WeekPlanResponse>> Handle(DismissPlanItemCommand command, CancellationToken cancellationToken) =>
        plans.Dismiss(command.UserId, command.ItemKey, command.Reason!.Value, cancellationToken);
}

/// <summary>Removes a purged account's plans and dismissals (ADR-0004). Safe to run twice.</summary>
internal sealed class DeleteRecommendationsOnUserDeleted(IRecommendationStore store) : IIntegrationEventHandler<UserDeleted>
{
    public Task Handle(UserDeleted integrationEvent, CancellationToken cancellationToken) =>
        store.DeleteForUser(integrationEvent.UserId, cancellationToken);
}
