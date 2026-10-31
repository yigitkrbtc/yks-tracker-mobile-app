using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Tracking.Application.Features.Sync;

namespace Nirengi.Modules.Tracking.Api.Endpoints;

internal static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder v1) =>
        v1.MapGet("/sync", async (ICurrentUser user, IDispatcher dispatcher, CancellationToken ct, long since = 0, int limit = GetChangesQuery.MaxLimit) =>
                (await dispatcher.Query(new GetChangesQuery(user.UserId, since, limit), ct)).Match(Results.Ok))
            .RequireAuthorization()
            .WithName("GetChanges")
            .WithSummary("The caller's records changed after the cursor, deletions included.")
            .Produces<SyncResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
}
