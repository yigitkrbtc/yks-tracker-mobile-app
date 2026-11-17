using System.Reflection;
using Nirengi.BuildingBlocks.Application.Events;
using Shouldly;

namespace Nirengi.ArchitectureTests;

/// <summary>
/// An event is stored in the outbox under a stable name, not its CLR type name, so moving or renaming the class
/// cannot strand waiting messages.
/// </summary>
public sealed class IntegrationEventNameTests
{
    private static List<Type> EventTypes() => ModuleAssemblies.Catalog
        .SelectMany(m => m.All)
        .Distinct()
        .SelectMany(a => a.GetTypes())
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IIntegrationEvent).IsAssignableFrom(t))
        .ToList();

    [Fact]
    public void Every_integration_event_carries_a_stable_name()
    {
        var events = EventTypes();

        events.ShouldNotBeEmpty();
        events.Where(t => t.GetCustomAttribute<IntegrationEventNameAttribute>(inherit: false) is null)
            .Select(t => t.FullName)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Event_names_are_unique_and_follow_the_module_dot_kebab_form()
    {
        var names = EventTypes()
            .Select(t => (Type: t, t.GetCustomAttribute<IntegrationEventNameAttribute>(inherit: false)?.Name))
            .Where(x => x.Name is not null)
            .ToList();

        names.GroupBy(x => x.Name).Where(g => g.Count() > 1).Select(g => g.Key).ShouldBeEmpty();
        names.Where(x => !System.Text.RegularExpressions.Regex.IsMatch(x.Name!, "^[a-z]+\\.[a-z0-9]+(-[a-z0-9]+)*$"))
            .Select(x => x.Type.FullName)
            .ShouldBeEmpty();
    }
}
