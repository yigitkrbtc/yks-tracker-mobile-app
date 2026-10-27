using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Identity.Application.Features.Profile;

namespace Nirengi.Modules.Identity.Api.Endpoints;

internal static class MeEndpoints
{
    public static void MapMeEndpoints(this IEndpointRouteBuilder v1)
    {
        var me = v1.MapGroup("/me").RequireAuthorization();

        me.MapGet("/", async (ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Query(new GetMeQuery(user.UserId), ct)).Match(Results.Ok))
            .WithName("GetMe")
            .Produces<UserProfile>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
