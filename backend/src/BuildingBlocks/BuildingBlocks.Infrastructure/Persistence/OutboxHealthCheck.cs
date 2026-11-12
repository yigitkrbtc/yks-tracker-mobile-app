using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Nirengi.BuildingBlocks.Application.Abstractions;

namespace Nirengi.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Degraded when the oldest unprocessed outbox message of a module is older than <see cref="OutboxOptions.StaleAfter"/>:
/// the worker is stuck or a handler keeps failing. Degraded keeps /health/ready at 200 (no restart) but surfaces the problem.
/// </summary>
public sealed class OutboxHealthCheck<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    IClock clock) : IHealthCheck
    where TContext : ModuleDbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var oldest = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Select(m => (DateTimeOffset?)m.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (oldest is { } at && clock.UtcNow - at > options.Value.StaleAfter)
        {
            return HealthCheckResult.Degraded(
                $"{typeof(TContext).Name} outbox: oldest unprocessed message is from {at:O}.");
        }

        return HealthCheckResult.Healthy();
    }
}
