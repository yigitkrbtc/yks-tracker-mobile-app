using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Identity.Domain.Tokens;

public enum RevokeReason
{
    Rotated = 0,
    ReuseDetected = 1,
    Logout = 2,
    PasswordReset = 3,
    AccountDeletion = 4,
    UserInactive = 5,
}

/// <summary>
/// Rotating refresh token. Every rotation stays in the sign-in's family and inherits its expiry,
/// so a family lives at most the refresh lifetime; presenting a rotated token revokes the family.
/// </summary>
public sealed class RefreshToken : Entity<Guid>
{
    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public RevokeReason? RevokeReason { get; private set; }

    public Guid? ReplacedById { get; private set; }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        var id = Guid.CreateVersion7(now);
        return new RefreshToken { Id = id, UserId = userId, FamilyId = id, TokenHash = tokenHash, CreatedAt = now, ExpiresAt = now + lifetime };
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public RefreshToken Rotate(string newTokenHash, DateTimeOffset now)
    {
        var next = new RefreshToken
        {
            Id = Guid.CreateVersion7(now),
            UserId = UserId,
            FamilyId = FamilyId,
            TokenHash = newTokenHash,
            CreatedAt = now,
            ExpiresAt = ExpiresAt,
        };
        Revoke(now, Tokens.RevokeReason.Rotated);
        ReplacedById = next.Id;
        return next;
    }

    public void Revoke(DateTimeOffset now, RevokeReason reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokeReason = reason;
    }
}
