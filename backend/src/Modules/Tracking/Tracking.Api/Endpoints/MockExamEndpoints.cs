using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Tracking.Application.Features.MockExams;
using Nirengi.Modules.Tracking.Domain.MockExams;

namespace Nirengi.Modules.Tracking.Api.Endpoints;

/// <param name="Sections">Only the solved sections; an unsolved one is left out, never sent as zero.</param>
/// <param name="Marks">Topic marks of wrong and blank answers; empty until the student tags them.</param>
/// <param name="ClientModifiedAt">When the student last edited it on the device; the newer edit wins.</param>
public sealed record SaveMockExamRequest(
    MockExamKind? Kind,
    DateOnly? TakenOn,
    Guid? SourceId,
    string? SourceName,
    string? Title,
    int? DurationMinutes,
    string? Note,
    IReadOnlyList<MockSectionInput>? Sections,
    IReadOnlyList<TopicMarkInput>? Marks,
    DateTimeOffset? ClientModifiedAt);

internal static class MockExamEndpoints
{
    public static void MapMockExamEndpoints(this IEndpointRouteBuilder v1)
    {
        var exams = v1.MapGroup("/mock-exams").RequireAuthorization();

        exams.MapPut("/{id:guid}", async (Guid id, SaveMockExamRequest request, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(
                    new SaveMockExamCommand(
                        user.UserId,
                        id,
                        request.Kind,
                        request.TakenOn,
                        request.SourceId,
                        request.SourceName,
                        request.Title,
                        request.DurationMinutes,
                        request.Note,
                        request.Sections,
                        request.Marks ?? [],
                        request.ClientModifiedAt),
                    ct)).Match(Results.Ok))
            .WithName("SaveMockExam")
            .WithSummary("Create or replace a mock exam, topic marks included, by its client id (idempotent).")
            .Produces<MockExamResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        exams.MapDelete("/{id:guid}", async (Guid id, DateTimeOffset modifiedAt, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(new DeleteMockExamCommand(user.UserId, id, modifiedAt), ct)).Match(stored => stored is null ? Results.NoContent() : Results.Ok(stored)))
            .WithName("DeleteMockExam")
            .WithSummary("Delete a mock exam (idempotent; a tombstone stays for sync). A newer stored edit wins: 200 with it.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<MockExamResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
