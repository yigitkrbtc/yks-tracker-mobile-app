using Microsoft.Extensions.Hosting;

namespace Nirengi.BuildingBlocks.Api.Security;

/// <summary>
/// Which environments may run relaxed (security audit, ADR-0021 G12). Only <c>Development</c> and the integration tests'
/// <c>Testing</c> are exempt; every other name (Production, Staging, a typo such as "production-eu") is treated as
/// production, so a mistyped <c>ASPNETCORE_ENVIRONMENT</c> fails closed instead of silently skipping the start-up
/// checks and serving the OpenAPI document.
/// </summary>
public static class RuntimeEnvironment
{
    public const string Testing = "Testing";

    public static bool IsProductionLike(this IHostEnvironment environment) =>
        !environment.IsDevelopment() && !environment.IsEnvironment(Testing);
}
