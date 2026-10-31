using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Tracking.Application.Features.TopicTests;
using Nirengi.Modules.Tracking.Domain.TopicTests;

namespace Nirengi.Modules.Tracking.Api.Endpoints;

/// <param name="ClientModifiedAt">When the student last edited it on the device; the newer edit wins.</param>
public sealed record SaveTopicTestRequest(
    DateOnly? StudiedOn,
    Guid? SourceId,
    string? SourceName,
    bool? IsRepeat,
    Difficulty? Difficulty,
    int? DurationMinutes,
    string? Note,
    IReadOnlyList<TopicResultInput>? Results,
    DateTimeOffset? ClientModifiedAt);

internal static class TopicTestEndpoints
{
    public static void MapTopicTestEndpoints(this IEndpointRouteBuilder v1)
    {
        var tests = v1.MapGroup("/topic-tests").RequireAuthorization();

        tests.MapPut("/{id:guid}", async (Guid id, SaveTopicTestRequest request, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(
                    new SaveTopicTestCommand(
                        user.UserId,
                        id,
                        request.StudiedOn,
                        request.SourceId,
                        request.SourceName,
                        request.IsRepeat,
                        request.Difficulty,
                        request.DurationMinutes,
                        request.Note,
                        request.Results,
                        request.ClientModifiedAt),
                    ct)).Match(Results.Ok))
            .WithName("SaveTopicTest")
            .WithSummary("Create or replace a topic test by its client id (idempotent).")
            .Produces<TopicTestResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tests.MapDelete("/{id:guid}", async (Guid id, DateTimeOffset modifiedAt, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(new DeleteTopicTestCommand(user.UserId, id, modifiedAt), ct)).Match(stored => stored is null ? Results.NoContent() : Results.Ok(stored)))
            .WithName("DeleteTopicTest")
            .WithSummary("Delete a topic test (idempotent; a tombstone stays for sync). A newer stored edit wins: 200 with it.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<TopicTestResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
