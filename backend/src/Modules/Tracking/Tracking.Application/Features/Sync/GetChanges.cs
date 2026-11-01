using FluentValidation;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Application.Features.MockExams;
using Nirengi.Modules.Tracking.Application.Features.Progress;
using Nirengi.Modules.Tracking.Application.Features.TopicTests;

namespace Nirengi.Modules.Tracking.Application.Features.Sync;

/// <summary>Everything of the caller's that changed after <paramref name="Since"/> (0 = from the start).</summary>
public sealed record GetChangesQuery(Guid UserId, long Since, int Limit) : IQuery<SyncResponse>
{
    public const int MaxLimit = 500;
}

/// <summary>One page of the change feed; every record kind has its own list, all sharing one cursor.</summary>
/// <param name="Cursor">Pass as <c>since</c> next time; unchanged when nothing new arrived.</param>
/// <param name="HasMore">More changes are waiting; ask again right away with the new cursor.</param>
public sealed record SyncResponse(
    IReadOnlyList<TopicTestResponse> TopicTests,
    IReadOnlyList<MockExamResponse> MockExams,
    IReadOnlyList<TopicProgressResponse> TopicProgress,
    long Cursor,
    bool HasMore);

internal sealed class GetChangesValidator : AbstractValidator<GetChangesQuery>
{
    public GetChangesValidator()
    {
        RuleFor(q => q.Since).GreaterThanOrEqualTo(0);
        RuleFor(q => q.Limit).InclusiveBetween(1, GetChangesQuery.MaxLimit);
    }
}

internal sealed class GetChangesHandler(ITopicTestRepository tests, IMockExamRepository exams, ITopicProgressRepository progress) : IQueryHandler<GetChangesQuery, SyncResponse>
{
    public async Task<Result<SyncResponse>> Handle(GetChangesQuery query, CancellationToken cancellationToken)
    {
        // Every kind draws positions from one sequence: take the first `limit` positions across them.
        var topicTests = await tests.GetChanges(query.UserId, query.Since, query.Limit + 1, cancellationToken);
        var mockExams = await exams.GetChanges(query.UserId, query.Since, query.Limit + 1, cancellationToken);
        var marks = await progress.GetChanges(query.UserId, query.Since, query.Limit + 1, cancellationToken);
        var page = topicTests.Select(t => t.ChangeSeq)
            .Concat(mockExams.Select(e => e.ChangeSeq))
            .Concat(marks.Select(p => p.ChangeSeq))
            .Order()
            .ToList();
        var taken = page.Take(query.Limit).ToList();
        var last = taken.Count == 0 ? query.Since : taken[^1];

        return new SyncResponse(
            [.. topicTests.Where(t => t.ChangeSeq <= last).Select(TopicTestResponse.From)],
            [.. mockExams.Where(e => e.ChangeSeq <= last).Select(MockExamResponse.From)],
            [.. marks.Where(p => p.ChangeSeq <= last).Select(TopicProgressResponse.From)],
            last,
            page.Count > query.Limit);
    }
}
