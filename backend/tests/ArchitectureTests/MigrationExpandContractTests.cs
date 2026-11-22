using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Shouldly;
using System.Reflection;

namespace Nirengi.ArchitectureTests;

/// <summary>
/// Expand/contract rule (ADR-0022 section 3): destructive schema operations (drop, rename, narrowing) are only allowed in a
/// migration whose source carries a <c>// contract-phase: reason</c> comment. N-1 code must keep working on the N schema.
/// </summary>
public sealed class MigrationExpandContractTests
{
    private const string ContractMarker = "// contract-phase:";

    /// <summary>
    /// Migrations written before the rule existed (ADR-0022, F2 card 6), keyed by migration id. A new entry means the rule is
    /// being bypassed: mark the migration with the comment instead.
    /// </summary>
    private static readonly HashSet<string> HistoricalExceptions =
    [
        // Drops attempt_window_started_at / failed_attempts_in_window; verification codes are short-lived, pre-release.
        "20261115120134_VerificationCodeEightDigits",
    ];

    public static TheoryData<string> MigrationIds() => new(AllMigrations().Select(m => m.Id));

    [Fact]
    public void Migration_catalog_is_not_empty()
    {
        AllMigrations().Count.ShouldBeGreaterThan(10);
    }

    [Theory]
    [MemberData(nameof(MigrationIds))]
    public void Destructive_migration_is_marked_contract_phase_or_a_known_exception(string migrationId)
    {
        var (type, id) = AllMigrations().Single(m => m.Id == migrationId);
        if (HistoricalExceptions.Contains(id))
        {
            return;
        }

        Violations(type, ReadSource(id))
            .ShouldBeEmpty($"{id}: add '{ContractMarker} <reason>' only if the previous API version no longer uses the column/table");
    }

    [Fact]
    public void Historical_exceptions_still_exist_and_are_still_destructive()
    {
        var all = AllMigrations().ToDictionary(m => m.Id, m => m.Type);
        foreach (var id in HistoricalExceptions)
        {
            all.ShouldContainKey(id);
            Violations(all[id], ReadSource(id)).ShouldNotBeEmpty($"{id} is no longer destructive; remove it from the exception list");
        }
    }

    [Fact]
    public void Audit_catches_destructive_operations_and_accepts_the_marker()
    {
        var unmarked = "class Sample { }";
        var marked = "class Sample { // contract-phase: column unused since v1.4\n }";

        var found = Violations(typeof(SampleDestructiveMigration), unmarked);

        found.Count.ShouldBe(5);
        found.ShouldContain(v => v.StartsWith("DropColumn"));
        found.ShouldContain(v => v.StartsWith("DropTable"));
        found.ShouldContain(v => v.StartsWith("RenameColumn"));
        found.ShouldContain(v => v.StartsWith("RenameTable"));
        found.ShouldContain(v => v.StartsWith("AlterColumn"));
        Violations(typeof(SampleDestructiveMigration), marked).ShouldBeEmpty();
        Violations(typeof(SampleSafeMigration), unmarked).ShouldBeEmpty();
    }

    private static List<string> Violations(Type migrationType, string source)
    {
        if (source.Contains(ContractMarker, StringComparison.Ordinal))
        {
            return [];
        }

        var migration = (Migration)Activator.CreateInstance(migrationType, nonPublic: true)!;
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(Migration).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);

        return builder.Operations.Select(Describe).OfType<string>().ToList();
    }

    private static string? Describe(MigrationOperation op) => op switch
    {
        DropColumnOperation d => $"DropColumn {d.Table}.{d.Name}",
        DropTableOperation d => $"DropTable {d.Name}",
        RenameColumnOperation r => $"RenameColumn {r.Table}.{r.Name} -> {r.NewName}",
        RenameTableOperation r => $"RenameTable {r.Name} -> {r.NewName}",
        AlterColumnOperation a when Narrows(a) => $"AlterColumn (narrowing) {a.Table}.{a.Name}",
        _ => null,
    };

    // shortcut: any type change counts as narrowing (widening cannot be told apart); mark it contract-phase.
    private static bool Narrows(AlterColumnOperation a)
    {
        var old = a.OldColumn;
        var typeChanged = a.ColumnType != old.ColumnType || (a.ColumnType is null && a.ClrType != old.ClrType);
        var nowRequired = !a.IsNullable && old.IsNullable;
        var shorter = a.MaxLength is { } now && (old.MaxLength is not { } before || now < before);
        return typeChanged || nowRequired || shorter;
    }

    private static List<(Type Type, string Id)> AllMigrations() =>
        ModuleAssemblies.Catalog
            .Select(m => m.Infrastructure)
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(Migration)))
            .Select(t => (t, t.GetCustomAttribute<MigrationAttribute>()!.Id))
            .OrderBy(m => m.Id)
            .ToList();

    private static string ReadSource(string id)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Nirengi.slnx")))
        {
            root = root.Parent;
        }

        root.ShouldNotBeNull("Nirengi.slnx not found above the test output directory");
        var file = Directory.EnumerateFiles(root.FullName, $"{id}.cs", SearchOption.AllDirectories).Single();
        return File.ReadAllText(file);
    }

    private sealed class SampleDestructiveMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("old", "t");
            migrationBuilder.DropTable("gone");
            migrationBuilder.RenameColumn("a", "t", "b");
            migrationBuilder.RenameTable("x", newName: "y");
            migrationBuilder.AlterColumn<string>("c", "t", type: "character varying(10)", maxLength: 10, nullable: false,
                oldClrType: typeof(string), oldType: "text", oldNullable: true);
        }
    }

    private sealed class SampleSafeMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>("n", "t", nullable: true);
            migrationBuilder.CreateIndex("ix", "t", "n");
        }
    }
}
