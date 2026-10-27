using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Identity.Application.Features.Deletion;

namespace Nirengi.Modules.Identity.Infrastructure;

public sealed class AccountPurgeOptions
{
    public const string SectionName = "Identity:Purge";

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>Runs the purge on a timer; the command is idempotent, so overlapping hosts are harmless.</summary>
internal sealed partial class AccountPurgeWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AccountPurgeOptions> options,
    ILogger<AccountPurgeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new PurgeDeletedAccountsCommand(), stoppingToken);
                if (result.IsSuccess && result.Value > 0)
                {
                    LogPurged(logger, result.Value);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPurgeFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} deleted accounts")]
    private static partial void LogPurged(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Account purge failed")]
    private static partial void LogPurgeFailed(ILogger logger, Exception exception);
}
