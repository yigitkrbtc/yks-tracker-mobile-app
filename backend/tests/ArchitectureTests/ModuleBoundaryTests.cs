using NetArchTest.Rules;
using Shouldly;

namespace Nirengi.ArchitectureTests;

/// <summary>Modules talk only through each other's Contracts (ADR-0001, ADR-0002).</summary>
public sealed class ModuleBoundaryTests
{
    [Theory]
    [MemberData(nameof(ModuleAssemblies.Names), MemberType = typeof(ModuleAssemblies))]
    public void Module_reaches_other_modules_only_through_contracts(string module)
    {
        var m = ModuleAssemblies.Get(module);
        var forbidden = ModuleAssemblies.Catalog
            .Where(other => other.Name != m.Name)
            .SelectMany(other => new[]
            {
                $"{other.Namespace}.Domain",
                $"{other.Namespace}.Engine",
                $"{other.Namespace}.Application",
                $"{other.Namespace}.Infrastructure",
                $"{other.Namespace}.Api",
            })
            .ToArray();

        var result = Types.InAssemblies(m.All)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(LayerTests.Describe(result));
    }

    [Fact]
    public void Analytics_engine_is_a_pure_library()
    {
        // ADR-0003: no I/O, no clock, no other Nirengi code. Inputs in, metrics out.
        var engine = ModuleAssemblies.Get("Analytics").Core;

        var result = Types.InAssembly(engine)
            .ShouldNot()
            .HaveDependencyOnAny(["Nirengi.BuildingBlocks", "Microsoft", "System.IO", "System.Net", "Npgsql"])
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(LayerTests.Describe(result));
        engine.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("Nirengi", StringComparison.Ordinal))
            .ShouldBeEmpty();
    }
}
