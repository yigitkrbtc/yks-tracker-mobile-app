using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Recommendation.Application.Features;
using Nirengi.Modules.Recommendation.Domain.Plans;

namespace Nirengi.Modules.Recommendation.Api.Endpoints;

public sealed record DismissPlanItemRequest(DismissReason? Reason);

internal static class RecommendationEndpoints
{
    public static void MapRecommendationEndpoints(this IEndpointRouteBuilder v1)
    {
        var recommendations = v1.MapGroup("/recommendations").RequireAuthorization();

        recommendations.MapGet("/week", async (HttpContext http, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Query(new GetWeekPlanQuery(user.UserId), ct)).Match(plan =>
                {
                    http.Response.Headers.ETag = plan.ETag;
                    http.Response.Headers.CacheControl = "private, no-cache";
                    return http.Request.Headers.IfNoneMatch.Contains(plan.ETag)
                        ? Results.StatusCode(StatusCodes.Status304NotModified)
                        : Results.Ok(plan);
                }))
            .WithName("GetWeekPlan")
            .WithSummary("This week's study plan with live progress; built on first read and frozen for the week. Honours If-None-Match.")
            .Produces<WeekPlanResponse>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        recommendations.MapPost("/items/{itemKey}/dismiss", async (string itemKey, DismissPlanItemRequest request, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(new DismissPlanItemCommand(user.UserId, itemKey, request.Reason), ct)).Match(Results.Ok))
            .WithName("DismissPlanItem")
            .WithSummary("Drop an item from this week's plan; the next candidate takes its slot. Idempotent.")
            .Produces<WeekPlanResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
