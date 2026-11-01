using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Recommendation.Application.Features;

namespace Nirengi.Modules.Recommendation.Application;

public static class RecommendationApplicationRegistration
{
    /// <summary>Services beyond the handlers that <c>AddApplicationHandlers</c> finds on its own.</summary>
    public static IServiceCollection AddRecommendationApplication(this IServiceCollection services) =>
        services.AddScoped<WeekPlanService>();
}
