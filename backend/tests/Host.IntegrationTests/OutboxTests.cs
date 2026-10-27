using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Nirengi.Modules.Platform.Infrastructure;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class OutboxTests(NirengiApiFactory factory)
{
    [Fact]
    public async Task Enqueued_event_commits_with_the_transaction_and_is_processed_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var probe = new ProbeEvent(Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.Enqueue(probe);
            await db.SaveChangesAsync(ct);
        }

        // The hosted worker polls every 200 ms in tests.
        OutboxMessage? stored = null;
        for (var i = 0; i < 50 && stored?.ProcessedAt is null; i++)
        {
            await Task.Delay(100, ct);
            await using var scope = factory.Services.CreateAsyncScope();
            stored = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().OutboxMessages.FindAsync([probe.Id], ct);
        }

        stored.ShouldNotBeNull();
        stored.ProcessedAt.ShouldNotBeNull();
        stored.Attempts.ShouldBe(0);
    }

    public sealed record ProbeEvent(Guid Id, DateTimeOffset OccurredAt) : IIntegrationEvent;
}
