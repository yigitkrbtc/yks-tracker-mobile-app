using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Curriculum.Application.Features;

namespace Nirengi.Modules.Curriculum.Api.Endpoints;

internal static class CurriculumEndpoints
{
    public static void MapCurriculumEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/curriculum", async (HttpContext http, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var version = await dispatcher.Query(new GetCurriculumVersionQuery(), ct);
                if (version.IsFailure)
                {
                    return version.ToProblem();
                }

                // The curriculum changes only on deploy; clients revalidate with the ETag instead of re-downloading.
                var etag = new EntityTagHeaderValue($"\"{version.Value}\"");
                http.Response.Headers.ETag = etag.ToString();
                http.Response.Headers.CacheControl = "private, no-cache";
                if (EntityTagHeaderValue.TryParseList(http.Request.Headers.IfNoneMatch, out var requested)
                    && requested.Any(t => t.Compare(etag, useStrongComparison: false) || t.Equals(EntityTagHeaderValue.Any)))
                {
                    return Results.StatusCode(StatusCodes.Status304NotModified);
                }

                return (await dispatcher.Query(new GetCurriculumQuery(), ct)).Match(Results.Ok);
            })
            .RequireAuthorization()
            .WithName("GetCurriculum")
            .Produces<CurriculumResponse>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
