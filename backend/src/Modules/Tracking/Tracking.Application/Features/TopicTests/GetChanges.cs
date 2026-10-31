using FluentValidation;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Tracking.Application.Abstractions;

namespace Nirengi.Modules.Tracking.Application.Features.TopicTests;

/// <summary>Everything of the caller's that changed after <paramref name="Since"/> (0 = from the start).</summary>
public sealed record GetChangesQuery(Guid UserId, long Since, int Limit) : IQuery<SyncResponse>
{
    public const int MaxLimit = 500;
}

/// <param name="Cursor">Pass as <c>since</c> next time; unchanged when nothing new arrived.</param>
/// <param name="HasMore">More changes are waiting; ask again right away with the new cursor.</param>
public sealed record SyncResponse(IReadOnlyList<TopicTestResponse> TopicTests, long Cursor, bool HasMore);

internal sealed class GetChangesValidator : AbstractValidator<GetChangesQuery>
{
    public GetChangesValidator()
    {
        RuleFor(q => q.Since).GreaterThanOrEqualTo(0);
        RuleFor(q => q.Limit).InclusiveBetween(1, GetChangesQuery.MaxLimit);
    }
}

internal sealed class GetChangesHandler(ITopicTestRepository tests) : IQueryHandler<GetChangesQuery, SyncResponse>
{
    public async Task<Result<SyncResponse>> Handle(GetChangesQuery query, CancellationToken cancellationToken)
    {
        var changes = await tests.GetChanges(query.UserId, query.Since, query.Limit + 1, cancellationToken);
        var page = changes.Take(query.Limit).ToList();
        return new SyncResponse(
            [.. page.Select(TopicTestResponse.From)],
            page.Count == 0 ? query.Since : page[^1].ChangeSeq,
            changes.Count > query.Limit);
    }
}
