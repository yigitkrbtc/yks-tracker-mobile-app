using System.Globalization;
using System.Reflection;

namespace Nirengi.BuildingBlocks.Application.RuleSets;

/// <summary>One parameter of a rule set as the admin panel shows it (read only, ADR-0019).</summary>
public sealed record RuleSetParameter(string Name, string Value);

/// <param name="Module">Owning module, e.g. <c>Analytics</c>.</param>
/// <param name="Version">The version stored with every result computed by these rules.</param>
public sealed record RuleSetDescription(string Module, string Name, string Version, IReadOnlyList<RuleSetParameter> Parameters)
{
    /// <summary>Every public instance property except <c>Version</c>, in declaration order.</summary>
    public static RuleSetDescription From<T>(string module, string name, string version, T ruleSet)
        where T : notnull
    {
        var parameters = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "Version" && p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract")
            .OrderBy(p => p.MetadataToken)
            .Select(p => new RuleSetParameter(p.Name, Format(p.GetValue(ruleSet))))
            .ToList();
        return new RuleSetDescription(module, name, version, parameters);
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        System.Collections.IEnumerable e and not string => string.Join(", ", e.Cast<object?>().Select(Format)),
        _ => value.ToString() ?? string.Empty,
    };
}

/// <summary>
/// Implemented by modules that own versioned rules. Platform lists all of them on <c>/v1/admin/rule-sets</c> without
/// knowing the modules; rules change only through code, the golden dataset and a version bump.
/// </summary>
public interface IRuleSetDescriptor
{
    RuleSetDescription Describe();
}
