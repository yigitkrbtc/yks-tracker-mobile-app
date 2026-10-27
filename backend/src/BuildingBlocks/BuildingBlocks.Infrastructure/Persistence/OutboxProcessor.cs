using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Events;

namespace Nirengi.BuildingBlocks.Infrastructure.Persistence;

public sealed class OutboxOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 50;

    public int MaxAttempts { get; set; } = 10;
}

/// <summary>
/// Drains one module's outbox and hands each event to every registered handler in the process.
/// Delivery is at-least-once: handlers must be idempotent.
/// </summary>
public sealed partial class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    IClock clock,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : ModuleDbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval);
        do
        {
            try
            {
                await ProcessBatch(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBatchFailed(logger, typeof(TContext).Name, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> ProcessBatch(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < options.Value.MaxAttempts)
            .OrderBy(m => m.OccurredAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await Publish(scope.ServiceProvider, message, cancellationToken);
                message.ProcessedAt = clock.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message;
                LogMessageFailed(logger, message.Id, message.Type, ex);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }

    private static Task Publish(IServiceProvider services, OutboxMessage message, CancellationToken cancellationToken)
    {
        var type = System.Type.GetType(message.Type, throwOnError: true)!;
        var integrationEvent = JsonSerializer.Deserialize(message.Payload, type)
            ?? throw new InvalidOperationException($"Outbox message {message.Id} has an empty payload.");

        return IntegrationEventPublisher.Publish(integrationEvent, type, services, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox batch for {Context} failed")]
    private static partial void LogBatchFailed(ILogger logger, string context, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} ({Type}) failed")]
    private static partial void LogMessageFailed(ILogger logger, Guid messageId, string type, Exception exception);
}
