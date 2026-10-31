using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Analytics.Application.Features;

namespace Nirengi.Modules.Analytics.Api.Endpoints;

internal static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder v1)
    {
        var analytics = v1.MapGroup("/analytics").RequireAuthorization();

        analytics.MapGet("/overview", async (HttpContext http, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Query(new GetAnalysisOverviewQuery(user.UserId), ct)).Match(overview =>
                {
                    http.Response.Headers.ETag = overview.ETag;
                    http.Response.Headers.CacheControl = "private, no-cache";
                    return http.Request.Headers.IfNoneMatch.Contains(overview.ETag)
                        ? Results.StatusCode(StatusCodes.Status304NotModified)
                        : Results.Ok(overview);
                }))
            .WithName("GetAnalysisOverview")
            .WithSummary("The caller's analysis panel; recomputed when records, profile or the day changed. Honours If-None-Match.")
            .Produces<AnalysisOverviewResponse>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        analytics.MapGet("/topics/{topicId:guid}", async (Guid topicId, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Query(new GetTopicAnalysisQuery(user.UserId, topicId), ct)).Match(Results.Ok))
            .WithName("GetTopicAnalysis")
            .WithSummary("The caller's topic card; a topic without records returns ConfidenceLevel None.")
            .Produces<TopicAnalysisResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
