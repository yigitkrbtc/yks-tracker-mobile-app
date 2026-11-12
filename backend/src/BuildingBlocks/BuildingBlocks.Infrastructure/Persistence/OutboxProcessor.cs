using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nirengi.BuildingBlocks.Application.Abstractions;

namespace Nirengi.BuildingBlocks.Infrastructure.Persistence;

public sealed class OutboxOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 50;

    /// <summary>First retry delay; doubles with every further failure up to <see cref="MaxDelay"/>.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromHours(1);

    /// <summary>From this many failed attempts on, every failure is logged at Error level. Messages are never abandoned.</summary>
    public int AlertAfterAttempts { get; set; } = 10;

    /// <summary>Processed messages older than this are deleted.</summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>The readiness check reports Degraded when the oldest unprocessed message is older than this.</summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Rejects settings that would make the worker spin (a zero poll interval or delay) or never run a batch; checked at startup.
/// </summary>
public sealed class OutboxOptionsValidator : IValidateOptions<OutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<string> failures = [];
        if (options.PollInterval <= TimeSpan.Zero)
        {
            failures.Add("Outbox:PollInterval must be greater than zero.");
        }

        if (options.BatchSize <= 0)
        {
            failures.Add("Outbox:BatchSize must be greater than zero.");
        }

        if (options.BaseDelay <= TimeSpan.Zero)
        {
            failures.Add("Outbox:BaseDelay must be greater than zero.");
        }

        if (options.MaxDelay < options.BaseDelay)
        {
            failures.Add("Outbox:MaxDelay must not be shorter than Outbox:BaseDelay.");
        }

        if (options.AlertAfterAttempts <= 0)
        {
            failures.Add("Outbox:AlertAfterAttempts must be greater than zero.");
        }

        if (options.RetentionPeriod <= TimeSpan.Zero)
        {
            failures.Add("Outbox:RetentionPeriod must be greater than zero.");
        }

        if (options.CleanupInterval <= TimeSpan.Zero)
        {
            failures.Add("Outbox:CleanupInterval must be greater than zero.");
        }

        if (options.StaleAfter <= TimeSpan.Zero)
        {
            failures.Add("Outbox:StaleAfter must be greater than zero.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

public static class OutboxBackoff
{
    /// <summary>Delay before the next attempt after <paramref name="attempts"/> failures: min(2^(attempts-1) * BaseDelay, MaxDelay).</summary>
    public static TimeSpan Delay(int attempts, OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var seconds = options.BaseDelay.TotalSeconds * Math.Pow(2, Math.Max(attempts - 1, 0));
        return TimeSpan.FromSeconds(Math.Min(seconds, options.MaxDelay.TotalSeconds));
    }
}

/// <summary>
/// Drains one module's outbox and hands each event to every registered handler in the process.
/// Delivery is at-least-once: handlers must be idempotent. A failed message is retried with
/// exponential backoff and never abandoned (ADR-0021). Rows are claimed with FOR UPDATE SKIP LOCKED,
/// so several instances can run side by side without delivering the same message twice at once.
/// </summary>
public sealed partial class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    IClock clock,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : ModuleDbContext
{
    private DateTimeOffset _lastCleanup = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval);
        do
        {
            try
            {
                await ProcessBatch(stoppingToken);

                if (clock.UtcNow - _lastCleanup >= options.Value.CleanupInterval)
                {
                    await DeleteProcessed(stoppingToken);
                    _lastCleanup = clock.UtcNow;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Only a requested stop ends the loop; any other failure (a timeout's cancellation included) is logged and retried next tick.
                LogBatchFailed(logger, typeof(TContext).Name, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> ProcessBatch(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        // The row locks live as long as this transaction; a crash mid-batch releases them and the batch is redelivered.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = clock.UtcNow;
        var messages = await db.OutboxMessages
            .FromSqlRaw(ClaimSql(db), now, settings.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                // Own scope per message: handlers get fresh DbContexts, independent of the claim transaction.
                await using var handlerScope = scopeFactory.CreateAsyncScope();
                await Publish(handlerScope.ServiceProvider, message, cancellationToken);
                message.ProcessedAt = clock.UtcNow;
                message.NextAttemptAt = null;
                message.LastError = null;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // A cancellation that is not a shutdown (a handler's own timeout) counts as this message's failure.
                message.Attempts++;
                message.NextAttemptAt = clock.UtcNow + OutboxBackoff.Delay(message.Attempts, settings);
                message.LastError = ex.Message;
                if (message.Attempts >= settings.AlertAfterAttempts)
                {
                    LogMessageStuck(logger, message.Id, message.Type, message.Attempts, ex);
                }
                else
                {
                    LogMessageFailed(logger, message.Id, message.Type, message.Attempts, ex);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages.Count;
    }

    /// <summary>Deletes messages processed longer ago than the retention period. Returns the number of rows removed.</summary>
    public async Task<int> DeleteProcessed(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var cutoff = clock.UtcNow - options.Value.RetentionPeriod;
        var deleted = await db.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
        {
            LogCleaned(logger, typeof(TContext).Name, deleted);
        }

        return deleted;
    }

    private static string ClaimSql(TContext db)
    {
        var entity = db.Model.FindEntityType(typeof(OutboxMessage))!;
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        string Column(string property) => Quote(entity.FindProperty(property)!.GetColumnName(table)!);

        var qualifiedTable = entity.GetSchema() is { } schema
            ? $"{Quote(schema)}.{Quote(table.Name)}"
            : Quote(table.Name);

        var processedAt = Column(nameof(OutboxMessage.ProcessedAt));
        var nextAttemptAt = Column(nameof(OutboxMessage.NextAttemptAt));
        var occurredAt = Column(nameof(OutboxMessage.OccurredAt));

        // {0} = now, {1} = batch size; FromSqlRaw turns them into parameters.
        return $$"""
            SELECT * FROM {{qualifiedTable}}
            WHERE {{processedAt}} IS NULL AND ({{nextAttemptAt}} IS NULL OR {{nextAttemptAt}} <= {0})
            ORDER BY {{occurredAt}}
            LIMIT {1}
            FOR UPDATE SKIP LOCKED
            """;
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static Task Publish(IServiceProvider services, OutboxMessage message, CancellationToken cancellationToken)
    {
        var type = System.Type.GetType(message.Type, throwOnError: true)!;
        var integrationEvent = JsonSerializer.Deserialize(message.Payload, type)
            ?? throw new InvalidOperationException($"Outbox message {message.Id} has an empty payload.");

        return IntegrationEventPublisher.Publish(integrationEvent, type, services, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox batch for {Context} failed")]
    private static partial void LogBatchFailed(ILogger logger, string context, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} ({Type}) failed, attempt {Attempts}")]
    private static partial void LogMessageFailed(ILogger logger, Guid messageId, string type, int attempts, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} ({Type}) is stuck: attempt {Attempts} failed")]
    private static partial void LogMessageStuck(ILogger logger, Guid messageId, string type, int attempts, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox cleanup for {Context} removed {Count} processed messages")]
    private static partial void LogCleaned(ILogger logger, string context, int count);
}
