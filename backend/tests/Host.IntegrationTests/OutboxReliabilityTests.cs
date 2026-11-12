using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>
/// Outbox delivery guarantees (ADR-0021, PR 2). Each test gets its own database on the shared container and
/// a hand-driven clock, so the real <see cref="OutboxProcessor{TContext}"/> runs without the host's background workers.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class OutboxReliabilityTests(NirengiApiFactory factory)
{
    private static readonly OutboxOptions Options = new()
    {
        BatchSize = 50,
        BaseDelay = TimeSpan.FromSeconds(5),
        MaxDelay = TimeSpan.FromMinutes(10),
        AlertAfterAttempts = 10,
        RetentionPeriod = TimeSpan.FromDays(7),
        StaleAfter = TimeSpan.FromMinutes(15),
    };

    [Fact]
    public async Task Message_is_delivered_after_more_failures_than_the_old_attempt_limit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await Harness.Create(factory, Options);
        harness.Handler.FailFirst = 15;
        var probe = await harness.Enqueue(ct);

        for (var round = 0; round < 40 && !await harness.IsProcessed(probe, ct); round++)
        {
            await harness.Processor.ProcessBatch(ct);
            harness.Clock.UtcNow += Options.MaxDelay + TimeSpan.FromSeconds(1);
        }

        var stored = await harness.Find(probe, ct);
        stored.ProcessedAt.ShouldNotBeNull();
        stored.Attempts.ShouldBe(15);
        stored.NextAttemptAt.ShouldBeNull();
        stored.LastError.ShouldBeNull();
        harness.Handler.Calls(probe).ShouldBe(16);
    }

    [Fact]
    public async Task Failed_message_waits_out_its_backoff_before_the_next_attempt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await Harness.Create(factory, Options);
        harness.Handler.FailFirst = 2;
        var probe = await harness.Enqueue(ct);

        (await harness.Processor.ProcessBatch(ct)).ShouldBe(1);
        var first = await harness.Find(probe, ct);
        first.Attempts.ShouldBe(1);
        first.NextAttemptAt.ShouldBe(harness.Clock.UtcNow + Options.BaseDelay);

        // Not due yet: neither immediately nor one tick before the deadline.
        (await harness.Processor.ProcessBatch(ct)).ShouldBe(0);
        harness.Clock.UtcNow += Options.BaseDelay - TimeSpan.FromMilliseconds(1);
        (await harness.Processor.ProcessBatch(ct)).ShouldBe(0);
        harness.Handler.Calls(probe).ShouldBe(1);

        // Due: retried, fails again, and the delay doubles.
        harness.Clock.UtcNow += TimeSpan.FromMilliseconds(1);
        (await harness.Processor.ProcessBatch(ct)).ShouldBe(1);
        var second = await harness.Find(probe, ct);
        second.Attempts.ShouldBe(2);
        second.NextAttemptAt.ShouldBe(harness.Clock.UtcNow + (2 * Options.BaseDelay));

        harness.Clock.UtcNow += 2 * Options.BaseDelay;
        (await harness.Processor.ProcessBatch(ct)).ShouldBe(1);
        (await harness.Find(probe, ct)).ProcessedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_stuck_message_does_not_hold_back_the_ones_behind_it()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await Harness.Create(factory, Options);
        var stuck = await harness.Enqueue(ct);
        harness.Handler.Poison.Add(stuck);
        harness.Clock.UtcNow += TimeSpan.FromSeconds(1);
        var behind = await harness.Enqueue(ct);

        await harness.Processor.ProcessBatch(ct);
        (await harness.Find(stuck, ct)).ProcessedAt.ShouldBeNull();
        (await harness.Find(behind, ct)).ProcessedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Processed_messages_older_than_the_retention_period_are_deleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await Harness.Create(factory, Options);
        var old = await harness.Enqueue(ct);
        await harness.Processor.ProcessBatch(ct);

        harness.Clock.UtcNow += TimeSpan.FromDays(8);
        var recent = await harness.Enqueue(ct);
        await harness.Processor.ProcessBatch(ct);
        harness.Handler.FailFirst = 1;
        var unprocessed = await harness.Enqueue(ct);
        await harness.Processor.ProcessBatch(ct);

        (await harness.Processor.DeleteProcessed(ct)).ShouldBe(1);

        (await harness.Exists(old, ct)).ShouldBeFalse();
        (await harness.Exists(recent, ct)).ShouldBeTrue();
        (await harness.Exists(unprocessed, ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Two_concurrent_batches_never_claim_the_same_message()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new OutboxOptions { BatchSize = 5 };
        await using var harness = await Harness.Create(factory, options);
        harness.Handler.Delay = TimeSpan.FromMilliseconds(300);

        var ids = new List<Guid>();
        for (var i = 0; i < 20; i++)
        {
            ids.Add(await harness.Enqueue(ct));
            harness.Clock.UtcNow += TimeSpan.FromMilliseconds(1);
        }

        var first = await Task.WhenAll(harness.Processor.ProcessBatch(ct), harness.Processor.ProcessBatch(ct));
        first.ShouldBe([5, 5]);

        while (await harness.Processor.ProcessBatch(ct) > 0)
        {
        }

        foreach (var id in ids)
        {
            harness.Handler.Calls(id).ShouldBe(1, $"message {id} must be delivered exactly once");
        }
    }

    [Fact]
    public async Task Readiness_check_is_degraded_only_when_the_oldest_unprocessed_message_is_stale()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await Harness.Create(factory, Options);
        var check = new OutboxHealthCheck<Harness.ProbeDbContext>(
            harness.Services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(Options),
            harness.Clock);

        (await check.CheckHealthAsync(new HealthCheckContext(), ct)).Status.ShouldBe(HealthStatus.Healthy);

        harness.Handler.FailFirst = 1;
        await harness.Enqueue(ct);
        await harness.Processor.ProcessBatch(ct);
        harness.Clock.UtcNow += Options.StaleAfter - TimeSpan.FromSeconds(1);
        (await check.CheckHealthAsync(new HealthCheckContext(), ct)).Status.ShouldBe(HealthStatus.Healthy);

        harness.Clock.UtcNow += TimeSpan.FromSeconds(2);
        (await check.CheckHealthAsync(new HealthCheckContext(), ct)).Status.ShouldBe(HealthStatus.Degraded);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(5, 80)]
    [InlineData(10, 600)]
    [InlineData(10_000, 600)]
    public void Backoff_doubles_up_to_the_cap(int attempts, int expectedSeconds)
    {
        OutboxBackoff.Delay(attempts, Options).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    public sealed record ProbeEvent(Guid Id, DateTimeOffset OccurredAt) : IIntegrationEvent;

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class ProbeHandler : IIntegrationEventHandler<ProbeEvent>
    {
        private readonly ConcurrentDictionary<Guid, int> _calls = new();

        /// <summary>Every delivery fails until the handler has been called this many times in total.</summary>
        public int FailFirst { get; set; }

        /// <summary>Messages that always fail.</summary>
        public ConcurrentBag<Guid> Poison { get; } = [];

        public TimeSpan Delay { get; set; }

        public int Calls(Guid id) => _calls.GetValueOrDefault(id);

        public async Task Handle(ProbeEvent integrationEvent, CancellationToken cancellationToken)
        {
            var calls = _calls.AddOrUpdate(integrationEvent.Id, 1, (_, n) => n + 1);
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            if (calls <= FailFirst || Poison.Contains(integrationEvent.Id))
            {
                throw new InvalidOperationException($"simulated outage, call {calls}");
            }
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _database;

        private Harness(string adminConnectionString, string database, ServiceProvider services, OutboxOptions options)
        {
            _adminConnectionString = adminConnectionString;
            _database = database;
            Services = services;
            Clock = (TestClock)services.GetRequiredService<IClock>();
            Handler = services.GetRequiredService<ProbeHandler>();
            Processor = new OutboxProcessor<ProbeDbContext>(
                services.GetRequiredService<IServiceScopeFactory>(),
                Microsoft.Extensions.Options.Options.Create(options),
                Clock,
                NullLogger<OutboxProcessor<ProbeDbContext>>.Instance);
        }

        public ServiceProvider Services { get; }

        public TestClock Clock { get; }

        public ProbeHandler Handler { get; }

        public OutboxProcessor<ProbeDbContext> Processor { get; }

        public static async Task<Harness> Create(NirengiApiFactory factory, OutboxOptions options)
        {
            var database = $"outbox_{Guid.NewGuid():N}";
            var admin = new NpgsqlConnectionStringBuilder(factory.ConnectionString);
            await using (var connection = new NpgsqlConnection(admin.ConnectionString))
            {
                await connection.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
                await create.ExecuteNonQueryAsync();
            }

            admin.Database = database;
            var services = new ServiceCollection()
                .AddSingleton<IClock, TestClock>()
                .AddSingleton<ProbeHandler>()
                .AddSingleton<IIntegrationEventHandler<ProbeEvent>>(sp => sp.GetRequiredService<ProbeHandler>())
                .AddDbContext<ProbeDbContext>(o => o.UseNpgsql(admin.ConnectionString).UseSnakeCaseNamingConvention())
                .BuildServiceProvider();

            await using (var scope = services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.EnsureCreatedAsync();
            }

            return new Harness(factory.ConnectionString, database, services, options);
        }

        public async Task<Guid> Enqueue(CancellationToken ct)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
            var probe = new ProbeEvent(Guid.CreateVersion7(), Clock.UtcNow);
            db.Enqueue(probe);
            await db.SaveChangesAsync(ct);
            return probe.Id;
        }

        public async Task<OutboxMessage> Find(Guid id, CancellationToken ct)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
            return await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id, ct);
        }

        public async Task<bool> Exists(Guid id, CancellationToken ct)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
            return await db.OutboxMessages.AnyAsync(m => m.Id == id, ct);
        }

        public async Task<bool> IsProcessed(Guid id, CancellationToken ct) => (await Find(id, ct)).ProcessedAt is not null;

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_database}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }

        public sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : ModuleDbContext(options)
        {
            protected override string Schema => "probe";
        }
    }
}
