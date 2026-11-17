using System.Reflection;
using Nirengi.BuildingBlocks.Application.Events;

namespace Nirengi.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Maps the stable outbox name of an integration event (<see cref="IntegrationEventNameAttribute"/>) to its CLR type.
/// Built once from every <c>Nirengi.*</c> assembly, the not yet loaded references of the loaded ones included.
/// </summary>
public static class IntegrationEventNames
{
    private const string AssemblyPrefix = "Nirengi.";

    private static readonly Lazy<IReadOnlyDictionary<string, Type>> Map = new(Build);

    /// <summary>The name an event is written under. Throws for an event type without <see cref="IntegrationEventNameAttribute"/>.</summary>
    public static string NameOf(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return eventType.GetCustomAttribute<IntegrationEventNameAttribute>(inherit: false)?.Name
            ?? throw new InvalidOperationException($"{eventType.FullName} needs an [IntegrationEventName] to be written to the outbox.");
    }

    /// <summary>The event type stored under <paramref name="name"/>; null for a legacy (type-name) row or an unknown name.</summary>
    public static Type? Resolve(string name) => Map.Value.GetValueOrDefault(name);

    /// <summary>Builds the map now so a duplicate name fails at startup instead of on the first delivery.</summary>
    public static void EnsureBuilt() => _ = Map.Value;

    private static Dictionary<string, Type> Build()
    {
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var assembly in LoadNirengiAssemblies())
        {
            foreach (var type in GetTypes(assembly))
            {
                if (type is not { IsClass: true, IsAbstract: false } || !typeof(IIntegrationEvent).IsAssignableFrom(type))
                {
                    continue;
                }

                var name = type.GetCustomAttribute<IntegrationEventNameAttribute>(inherit: false)?.Name;
                if (name is null)
                {
                    continue;
                }

                if (!map.TryAdd(name, type))
                {
                    throw new InvalidOperationException(
                        $"Integration event name '{name}' is used by both {map[name].FullName} and {type.FullName}.");
                }
            }
        }

        return map;
    }

    private static IEnumerable<Type> GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    private static List<Assembly> LoadNirengiAssemblies()
    {
        var found = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var queue = new Queue<Assembly>(AppDomain.CurrentDomain.GetAssemblies().Where(IsNirengi));
        while (queue.TryDequeue(out var assembly))
        {
            if (!found.TryAdd(assembly.FullName!, assembly))
            {
                continue;
            }

            // An event's assembly may not be loaded yet (the runtime loads references lazily), so follow the references.
            foreach (var reference in assembly.GetReferencedAssemblies().Where(r => r.Name!.StartsWith(AssemblyPrefix, StringComparison.Ordinal)))
            {
                queue.Enqueue(Assembly.Load(reference));
            }
        }

        return [.. found.Values];
    }

    private static bool IsNirengi(Assembly assembly) =>
        !assembly.IsDynamic && assembly.GetName().Name!.StartsWith(AssemblyPrefix, StringComparison.Ordinal);
}
