using System.Reflection;
using Shouldly;

namespace Nirengi.ArchitectureTests;

/// <summary>
/// ADR-0004: a student's data belongs to the student. There is no global query filter; the guarantee is the shape of the
/// repository: every method that reads or deletes a user's records names the owner (<c>userId</c>), so a handler cannot load
/// another student's row by id and forget to compare owners.
/// </summary>
public sealed class OwnershipTests
{
    private static readonly string[] UserDataModules = ["Tracking", "Analytics", "Recommendation"];

    /// <summary>Methods that take the whole entity: the entity carries its owner, and the write path never reads by id.</summary>
    private static readonly string[] EntityWrites = ["Add", "Replace", "SavePlan"];

    public static TheoryData<string> Modules() => new(UserDataModules);

    [Theory]
    [MemberData(nameof(Modules))]
    public void User_data_repository_methods_name_the_owner(string module)
    {
        var m = ModuleAssemblies.Get(module);

        var offenders = m.Application.GetTypes()
            .Where(t => t.IsInterface && t.Namespace!.EndsWith(".Application.Abstractions", StringComparison.Ordinal)
                && (t.Name.EndsWith("Repository", StringComparison.Ordinal) || t.Name.EndsWith("Store", StringComparison.Ordinal)))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(method => (Type: t, Method: method)))
            .Where(x => !EntityWrites.Contains(x.Method.Name)
                && !x.Method.GetParameters().Any(p => p.Name == "userId" && p.ParameterType == typeof(Guid)))
            .Select(x => $"{x.Type.Name}.{x.Method.Name}")
            .ToList();

        offenders.ShouldBeEmpty("These repository methods can reach another user's rows (ADR-0004): " + string.Join(", ", offenders));
    }

    [Fact]
    public void Every_user_data_module_has_a_repository_to_check()
    {
        foreach (var module in UserDataModules)
        {
            ModuleAssemblies.Get(module).Application.GetTypes()
                .Count(t => t.IsInterface && (t.Name.EndsWith("Repository", StringComparison.Ordinal) || t.Name.EndsWith("Store", StringComparison.Ordinal)))
                .ShouldBeGreaterThan(0, module);
        }
    }
}
