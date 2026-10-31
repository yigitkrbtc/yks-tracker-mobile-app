using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Analytics.Application;
using Nirengi.Modules.Analytics.Application.Abstractions;
using Nirengi.Modules.Analytics.Infrastructure.Persistence;

namespace Nirengi.Modules.Analytics.Infrastructure;

public static class AnalyticsInfrastructureRegistration
{
    public static IServiceCollection AddAnalyticsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<AnalyticsDbContext>(configuration, AnalyticsDbContext.SchemaName, Application.AssemblyReference.Assembly);
        services.AddAnalyticsApplication();
        services.AddScoped<IAnalyticsStore, AnalyticsStore>();
        return services;
    }
}
