namespace Nirengi.BuildingBlocks.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Payload { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    /// <summary>Earliest time the next delivery attempt may run; null means immediately.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    public string? LastError { get; set; }
}
