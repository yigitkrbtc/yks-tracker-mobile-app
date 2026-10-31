using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Analytics.Application.Features;

namespace Nirengi.Modules.Analytics.Application;

public static class AnalyticsApplicationRegistration
{
    /// <summary>Services beyond the handlers that <c>AddApplicationHandlers</c> finds on its own.</summary>
    public static IServiceCollection AddAnalyticsApplication(this IServiceCollection services) =>
        services.AddScoped<IAnalysisRefresher, AnalysisRefresher>();
}
