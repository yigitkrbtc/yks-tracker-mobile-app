using NetArchTest.Rules;
using Shouldly;

namespace Nirengi.ArchitectureTests;

/// <summary>Clean Architecture dependency rule inside each module (ADR-0001).</summary>
public sealed class LayerTests
{
    private static readonly string[] Frameworks =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
    ];

    [Theory]
    [MemberData(nameof(ModuleAssemblies.Names), MemberType = typeof(ModuleAssemblies))]
    public void Core_does_not_depend_on_outer_layers_or_frameworks(string module)
    {
        var m = ModuleAssemblies.Get(module);

        var result = Types.InAssembly(m.Core)
            .ShouldNot()
            .HaveDependencyOnAny([$"{m.Namespace}.Application", $"{m.Namespace}.Infrastructure", $"{m.Namespace}.Api", .. Frameworks])
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblies.Names), MemberType = typeof(ModuleAssemblies))]
    public void Application_does_not_depend_on_infrastructure_api_or_frameworks(string module)
    {
        var m = ModuleAssemblies.Get(module);

        var result = Types.InAssembly(m.Application)
            .ShouldNot()
            .HaveDependencyOnAny([$"{m.Namespace}.Infrastructure", $"{m.Namespace}.Api", .. Frameworks])
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblies.Names), MemberType = typeof(ModuleAssemblies))]
    public void Contracts_expose_no_internals(string module)
    {
        var m = ModuleAssemblies.Get(module);

        var result = Types.InAssembly(m.Contracts)
            .ShouldNot()
            .HaveDependencyOnAny([$"{m.Namespace}.Domain", $"{m.Namespace}.Engine", $"{m.Namespace}.Application", $"{m.Namespace}.Infrastructure", .. Frameworks])
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Building_block_domain_is_framework_free()
    {
        var result = Types.InAssembly(typeof(BuildingBlocks.Domain.Results.Result).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(["Nirengi.BuildingBlocks.Application", "Nirengi.BuildingBlocks.Infrastructure", "Nirengi.BuildingBlocks.Api", .. Frameworks])
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    internal static string Describe(NetArchTest.Rules.TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
