using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;

namespace Nirengi.BuildingBlocks.Infrastructure;

public static class ModuleInfrastructureRegistration
{
    public const string ConnectionStringName = "Database";

    /// <summary>
    /// Wires a module's DbContext (own schema + migrations history), its outbox worker,
    /// and the handlers in its Application assembly.
    /// </summary>
    public static IServiceCollection AddModuleInfrastructure<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema,
        Assembly applicationAssembly)
        where TContext : ModuleDbContext
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing.");

        services.AddDbContext<TContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IEventOutbox>(sp => sp.GetRequiredService<TContext>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddOptions<OutboxOptions>().BindConfiguration("Outbox");
        services.AddHostedService<OutboxProcessor<TContext>>();
        services.AddApplicationHandlers(applicationAssembly);
        return services;
    }
}
