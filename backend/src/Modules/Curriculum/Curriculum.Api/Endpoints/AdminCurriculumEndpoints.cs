using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Api.Security;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Curriculum.Application.Features.Admin;
using Nirengi.Modules.Curriculum.Domain.Versions;

namespace Nirengi.Modules.Curriculum.Api.Endpoints;

/// <param name="FromVersion">Null: empty draft (or the open one). A number: a draft back to that version.</param>
public sealed record CreateDraftRequest(int? FromVersion);

/// <param name="Revision">The revision the editor loaded; a newer one on the server answers 409.</param>
public sealed record SaveDraftRequest(int Revision, string Title, IReadOnlyList<CurriculumOperation> Operations);

public sealed record PublishDraftRequest(int Revision, string Summary);

/// <summary>Curriculum versions for the admin panel (ADR-0019). Admin role, per-user rate limit.</summary>
internal static class AdminCurriculumEndpoints
{
    public static void MapAdminCurriculumEndpoints(this IEndpointRouteBuilder v1)
    {
        var admin = v1.MapGroup("/admin/curriculum")
            .RequireAuthorization(Policies.Admin)
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .WithTags("Admin")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        admin.MapGet("/", async (IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Query(new GetAdminCurriculumQuery(), ct)).Match(Results.Ok))
            .WithName("GetAdminCurriculum")
            .WithSummary("Live curriculum with retired topics, weights per test and open drafts.")
            .Produces<AdminCurriculumResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapGet("/versions", async (IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Query(new GetVersionsQuery(), ct)).Match(Results.Ok))
            .WithName("GetCurriculumVersions")
            .Produces<IReadOnlyList<VersionSummaryDto>>();

        admin.MapGet("/versions/{number:int}", async (int number, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Query(new GetVersionQuery(number), ct)).Match(Results.Ok))
            .WithName("GetCurriculumVersion")
            .Produces<VersionDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/drafts", async (CreateDraftRequest request, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(new CreateDraftCommand(user.UserId, request.FromVersion), ct)).Match(Results.Ok))
            .WithName("CreateCurriculumDraft")
            .WithSummary("Opens the manual draft (returns the existing one if open), or a draft back to an earlier version.")
            .Produces<DraftResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapGet("/drafts/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Query(new GetDraftQuery(id), ct)).Match(Results.Ok))
            .WithName("GetCurriculumDraft")
            .WithSummary("The draft with a dry-run validation against the live curriculum.")
            .Produces<DraftResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPut("/drafts/{id:guid}", async (Guid id, SaveDraftRequest request, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(new SaveDraftCommand(id, request.Revision, request.Title, request.Operations), ct)).Match(Results.Ok))
            .WithName("SaveCurriculumDraft")
            .WithSummary("Replaces the operation list. Assigns codes to new topics and moves the draft onto the current version.")
            .Produces<DraftResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPost("/drafts/{id:guid}/publish", async (Guid id, PublishDraftRequest request, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(new PublishDraftCommand(user.UserId, id, request.Revision, request.Summary), ct)).Match(Results.Ok))
            .WithName("PublishCurriculumDraft")
            .WithSummary("Applies the draft in one transaction as the next version. 409 if the draft or the curriculum moved on.")
            .Produces<VersionSummaryDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapDelete("/drafts/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var deleted = await dispatcher.Send(new DeleteDraftCommand(id), ct);
                return deleted.IsSuccess ? Results.NoContent() : deleted.ToProblem();
            })
            .WithName("DeleteCurriculumDraft")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapGet("/export", async (IDispatcher dispatcher, IClock clock, CancellationToken ct) =>
                (await dispatcher.Query(new ExportSeedQuery(), ct)).Match(zip =>
                {
                    var day = clock.UtcNow.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                    return Results.File(zip, "application/zip", $"nirengi-mufredat-{day}.zip");
                }))
            .WithName("ExportCurriculumSeed")
            .WithSummary("The live curriculum as data/seed files, to commit back so a fresh database starts from it.")
            .Produces<byte[]>(StatusCodes.Status200OK, "application/zip")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
