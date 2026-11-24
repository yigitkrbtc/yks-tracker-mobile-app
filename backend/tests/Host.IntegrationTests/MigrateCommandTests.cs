using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Nirengi.Host.IntegrationTests;

/// <summary>ADR-0022: <c>--migrate</c> applies every module's migrations, seeds nothing, and exits without serving.</summary>
/// <summary>
/// Runs alone: Program is invoked in-process, and a WebApplicationFactory resolving in parallel subscribes a
/// process-wide HostFactoryResolver listener that hijacks any host built meanwhile (our args were dropped for
/// appsettings' localhost:5432). Console.SetError is process-global too.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MigrateCommandGroup
{
    public const string Name = "migrate-command";
}

[Trait("Category", "Integration")]
[Collection(MigrateCommandGroup.Name)]
public sealed class MigrateCommandTests : IAsyncLifetime
{
    private readonly StringWriter _stderr = new();
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Migrate_argument_applies_all_migrations_and_returns_zero_without_starting_the_web_host()
    {
        // A web host would block in RunAsync; getting an exit code back is the proof that none was started.
        var exitCode = await RunHost("--migrate", $"--ConnectionStrings:Database={_postgres.GetConnectionString()}");

        exitCode.ShouldBe(0, _stderr.ToString());
        (await Scalar("SELECT count(*) FROM information_schema.tables WHERE table_name = 'outbox_messages'")).ShouldBe(7L);
        (await Scalar("SELECT count(*) FROM curriculum.topics")).ShouldBe(0L); // reference data is the API's job
    }

    [Fact]
    public async Task Migrate_argument_returns_non_zero_when_the_database_is_unreachable()
    {
        var exitCode = await RunHost("--migrate", "--ConnectionStrings:Database=Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2");

        exitCode.ShouldNotBe(0);
    }

    private async Task<int> RunHost(params string[] extra)
    {
        string[] args =
        [
            "--environment=Testing",
            $"--Jwt:SigningKey={NirengiApiFactory.TestSigningKey}",
            $"--Identity:Security:HashKey={NirengiApiFactory.TestHashKey}",
            "--Smtp:Host=localhost",
            "--Smtp:FromAddress=test@nirengi.local",
            .. extra,
        ];
        var entryPoint = typeof(Program).Assembly.EntryPoint!;
        // The compiler's entry point for an async top-level program is a synchronous wrapper returning int.
        // Console.Error is captured so a failing migration shows its reason in the assertion message.
        var original = Console.Error;
        Console.SetError(_stderr);
        try
        {
            return await Task.Run(() => (int)entryPoint.Invoke(null, [args])!);
        }
        finally
        {
            Console.SetError(original);
        }
    }

    private async Task<long> Scalar(string sql)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
