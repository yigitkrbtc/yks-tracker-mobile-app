using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Shouldly;

namespace Nirengi.ArchitectureTests;

/// <summary>
/// A module registered with <c>publishesEvents: false</c> has no outbox worker, so an event it enqueues would sit in its
/// outbox forever. The module's registration and its code must agree: it writes events exactly when it publishes them.
/// </summary>
public sealed class EventPublishingTests
{
    [Theory]
    [MemberData(nameof(ModuleAssemblies.Names), MemberType = typeof(ModuleAssemblies))]
    public void Module_runs_an_outbox_worker_exactly_when_its_code_writes_events(string module)
    {
        var m = ModuleAssemblies.Get(module);

        // Api is scanned too: Engagement and Platform register their infrastructure there.
        var writers = new[] { m.Application, m.Infrastructure, m.Api }.SelectMany(EventWriters).ToList();

        if (RunsOutboxWorker(m.Api))
        {
            writers.ShouldNotBeEmpty($"{module} runs an outbox worker but writes no events; register it with publishesEvents: false.");
        }
        else
        {
            writers.ShouldBeEmpty($"{module} is registered with publishesEvents: false, so these events would never be published.");
        }
    }

    private static bool RunsOutboxWorker(Assembly api)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = "Host=localhost" })
            .Build();
        var services = new ServiceCollection();
        var module = (IModule)Activator.CreateInstance(api.GetTypes().Single(t => typeof(IModule).IsAssignableFrom(t)))!;

        module.AddServices(services, configuration);

        return services.Any(d => d.ServiceType == typeof(IHostedService)
            && d.ImplementationType is { IsGenericType: true } type
            && type.GetGenericTypeDefinition() == typeof(OutboxProcessor<>));
    }

    /// <summary>
    /// Types that implement or reference <see cref="IEventOutbox"/>, or call any <c>Enqueue(IIntegrationEvent)</c>
    /// (the outbox, a unit of work, or <c>ModuleDbContext</c> directly). Includes compiler-generated state machines.
    /// </summary>
    private static IEnumerable<string> EventWriters(Assembly assembly)
    {
        var outbox = typeof(IEventOutbox).FullName;
        var integrationEvent = typeof(IIntegrationEvent).FullName;
        using var definition = AssemblyDefinition.ReadAssembly(assembly.Location);

        foreach (var type in definition.MainModule.GetTypes())
        {
            if (type.Interfaces.Any(i => i.InterfaceType.FullName == outbox))
            {
                yield return $"{type.FullName} implements IEventOutbox";
            }

            foreach (var method in type.Methods.Where(x => x.HasBody))
            {
                var call = method.Body.Instructions
                    .Select(i => i.Operand)
                    .OfType<MethodReference>()
                    .FirstOrDefault(r => r.DeclaringType.FullName == outbox
                        || (r.Name == "Enqueue" && r.Parameters.Count == 1 && r.Parameters[0].ParameterType.FullName == integrationEvent));
                if (call is not null)
                {
                    yield return $"{type.FullName}.{method.Name} calls {call.DeclaringType.Name}.{call.Name}";
                }
            }
        }
    }
}
