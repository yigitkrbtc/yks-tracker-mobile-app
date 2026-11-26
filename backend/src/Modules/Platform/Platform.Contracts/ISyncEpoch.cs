namespace Nirengi.Modules.Platform.Contracts;

/// <summary>
/// Identity of the server's change-feed history (ADR-0022 §4). A database restore moves change sequences back, so it
/// renews the epoch; a device that sees a new one resets its cursor and queues its records again.
/// </summary>
public interface ISyncEpoch
{
    Task<Guid> Current(CancellationToken cancellationToken);
}
