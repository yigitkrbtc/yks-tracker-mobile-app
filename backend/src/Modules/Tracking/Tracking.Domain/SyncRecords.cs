namespace Nirengi.Modules.Tracking.Domain;

/// <summary>A record that travels through the offline sync (ADR-0014): owned by a user, placed in the change feed.</summary>
public interface ISyncRecord
{
    Guid UserId { get; }

    long ChangeSeq { get; }

    void AssignChangeSeq(long changeSeq);
}

/// <summary>The edit time rule shared by every synced record (last writer by edit time wins, ADR-0005).</summary>
public static class EditTime
{
    /// <summary>Capped at server time (a fast device clock cannot win forever), cut to PostgreSQL's microseconds.</summary>
    public static DateTimeOffset Of(DateTimeOffset client, DateTimeOffset now)
    {
        var capped = client > now ? now : client;
        return capped.AddTicks(-(capped.UtcTicks % 10));
    }
}
