using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Security;
using Nirengi.BuildingBlocks.Application.RuleSets;

namespace Nirengi.Modules.Platform.Api.Endpoints;

/// <summary>Read-only view of every module's versioned rules (ADR-0019); editing them stays a code change.</summary>
internal static class RuleSetEndpoints
{
    public static void MapRuleSets(this IEndpointRouteBuilder v1) =>
        v1.MapGet("/admin/rule-sets", (IEnumerable<IRuleSetDescriptor> descriptors) =>
                Results.Ok(descriptors.Select(d => d.Describe()).OrderBy(d => d.Module, StringComparer.Ordinal).ToList()))
            .RequireAuthorization(Policies.Admin)
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .WithTags("Admin")
            .WithName("GetRuleSets")
            .Produces<IReadOnlyList<RuleSetDescription>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
