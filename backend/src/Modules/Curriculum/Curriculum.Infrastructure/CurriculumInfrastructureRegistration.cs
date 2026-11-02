using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Curriculum.Application.Abstractions;
using Nirengi.Modules.Curriculum.Contracts;
using Nirengi.Modules.Curriculum.Infrastructure.Persistence;
using Nirengi.Modules.Curriculum.Infrastructure.Seed;

namespace Nirengi.Modules.Curriculum.Infrastructure;

public static class CurriculumInfrastructureRegistration
{
    public static IServiceCollection AddCurriculumInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<CurriculumDbContext>(configuration, CurriculumDbContext.SchemaName, Application.AssemblyReference.Assembly);
        services.AddScoped<ICurriculumRepository, CurriculumRepository>();
        services.AddScoped<ICurriculumCatalog, CurriculumCatalog>();
        services.Configure<CurriculumOptions>(configuration.GetSection(CurriculumOptions.Section));
        services.AddScoped<CurriculumPublisher>();
        services.AddScoped<CurriculumSeeder>();
        services.AddScoped<ICurriculumAdminStore, CurriculumAdminStore>();
        services.AddScoped<IExamCalendarSource, StoredExamCalendarSource>();
        return services;
    }
}
