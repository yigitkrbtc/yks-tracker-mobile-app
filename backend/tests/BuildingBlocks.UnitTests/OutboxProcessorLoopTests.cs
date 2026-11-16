using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Shouldly;

namespace Nirengi.BuildingBlocks.UnitTests;

public sealed class OutboxProcessorLoopTests
{
    [Fact]
    public async Task A_cancellation_that_is_not_a_shutdown_does_not_end_the_worker_loop()
    {
        var ct = TestContext.Current.CancellationToken;
        var scopes = new ThrowingScopeFactory(() => new OperationCanceledException("a command timed out"));
        var processor = new OutboxProcessor<NoDbContext>(
            scopes,
            Options.Create(new OutboxOptions { PollInterval = TimeSpan.FromMilliseconds(10) }),
            new FixedClock(),
            NullLogger<OutboxProcessor<NoDbContext>>.Instance);

        await processor.StartAsync(ct);
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (scopes.Calls < 3 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10, ct);
        }

        // Several ticks after the first failure: the loop is alive, and the host would not have been stopped.
        scopes.Calls.ShouldBeGreaterThanOrEqualTo(3);
        processor.ExecuteTask!.IsCompleted.ShouldBeFalse();

        await processor.StopAsync(ct);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class ThrowingScopeFactory(Func<Exception> failure) : IServiceScopeFactory
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _calls);
            throw failure();
        }
    }

    private sealed class NoDbContext(DbContextOptions options) : ModuleDbContext(options)
    {
        protected override string Schema => "none";
    }
}
