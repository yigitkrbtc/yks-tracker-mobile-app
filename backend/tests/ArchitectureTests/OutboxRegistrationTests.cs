using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Identity.Infrastructure;
using Nirengi.Modules.Tracking.Infrastructure;
using Shouldly;

namespace Nirengi.ArchitectureTests;

/// <summary>
/// Every module has its own outbox (ADR-0002). A container-wide <see cref="IEventOutbox"/> would resolve to whichever
/// module registered last and queue the event on the wrong module's DbContext, outside the handler's transaction.
/// Handlers use their module's unit of work instead.
/// </summary>
public sealed class OutboxRegistrationTests
{
    [Fact]
    public void Module_infrastructure_registers_no_container_wide_event_outbox()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = "Host=localhost" })
            .Build();
        var services = new ServiceCollection();

        services.AddModuleInfrastructure<IdentityDbContext>(configuration, "identity", Modules.Identity.Application.AssemblyReference.Assembly);
        services.AddModuleInfrastructure<TrackingDbContext>(configuration, "tracking", Modules.Tracking.Application.AssemblyReference.Assembly);

        services.Where(d => d.ServiceType == typeof(IEventOutbox)).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblies.Names), MemberType = typeof(ModuleAssemblies))]
    public void Handlers_do_not_inject_the_generic_event_outbox(string module)
    {
        var m = ModuleAssemblies.Get(module);

        var offenders = new[] { m.Application, m.Infrastructure }
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IEventOutbox))))
            .Select(t => t.FullName)
            .ToList();

        offenders.ShouldBeEmpty();
    }
}
