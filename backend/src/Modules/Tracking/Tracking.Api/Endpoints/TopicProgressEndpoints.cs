using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Tracking.Application.Features.Progress;
using Nirengi.Modules.Tracking.Domain.Progress;

namespace Nirengi.Modules.Tracking.Api.Endpoints;

/// <param name="ClientModifiedAt">When the student last changed the mark on the device; the newer edit wins.</param>
public sealed record SaveTopicProgressRequest(TopicProgressStatus? Status, DateTimeOffset? ClientModifiedAt);

internal static class TopicProgressEndpoints
{
    public static void MapTopicProgressEndpoints(this IEndpointRouteBuilder v1) =>
        v1.MapPut("/topic-progress/{topicId:guid}", async (Guid topicId, SaveTopicProgressRequest request, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(new SaveTopicProgressCommand(user.UserId, topicId, request.Status, request.ClientModifiedAt), ct)).Match(Results.Ok))
            .RequireAuthorization()
            .WithName("SaveTopicProgress")
            .WithSummary("Set the caller's own mark on a topic (idempotent; the newer edit wins). Never used by the metrics.")
            .Produces<TopicProgressResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
