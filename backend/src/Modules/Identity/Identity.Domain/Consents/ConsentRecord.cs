using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Identity.Domain.Consents;

public enum ConsentKind
{
    Terms = 0,
    PrivacyNotice = 1,
    Marketing = 2,
}

/// <summary>Append-only record of which text version the user accepted (or declined) and when.</summary>
public sealed class ConsentRecord : Entity<Guid>
{
    private ConsentRecord()
    {
    }

    public Guid UserId { get; private set; }

    public ConsentKind Kind { get; private set; }

    public string Version { get; private set; } = null!;

    public bool Granted { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public static ConsentRecord Record(Guid userId, ConsentKind kind, string version, bool granted, DateTimeOffset now) =>
        new() { Id = Guid.CreateVersion7(now), UserId = userId, Kind = kind, Version = version, Granted = granted, RecordedAt = now };
}
