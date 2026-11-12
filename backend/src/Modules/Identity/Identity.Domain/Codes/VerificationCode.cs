using System.Security.Cryptography;
using System.Text;
using Nirengi.BuildingBlocks.Domain.Entities;
using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.Modules.Identity.Domain.Codes;

public enum CodePurpose
{
    EmailVerification = 0,
    PasswordReset = 1,
}

/// <summary>
/// One live 6-digit code per user and purpose; issuing a new one replaces the old. At most
/// <see cref="MaxIssuesPerWindow"/> codes per <see cref="IssueWindow"/>, so fresh codes cannot be used to keep
/// guessing (each code allows a few attempts): 5 codes x 5 attempts = 25 guesses a day out of a million.
/// </summary>
public sealed class VerificationCode : Entity<Guid>
{
    public const int MaxIssuesPerWindow = 5;

    public static readonly TimeSpan IssueWindow = TimeSpan.FromHours(24);

    private VerificationCode()
    {
    }

    public Guid UserId { get; private set; }

    public CodePurpose Purpose { get; private set; }

    public string CodeHash { get; private set; } = null!;

    public DateTimeOffset SentAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>Start of the current issue window; null before the first issue counted (rows older than ADR-0021).</summary>
    public DateTimeOffset? IssueWindowStartedAt { get; private set; }

    public int IssuesInWindow { get; private set; }

    public static VerificationCode Issue(Guid userId, CodePurpose purpose, string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        var code = new VerificationCode { Id = Guid.CreateVersion7(now), UserId = userId, Purpose = purpose };
        code.Reissue(codeHash, now, lifetime);
        return code;
    }

    public void Reissue(string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (IssueWindowStartedAt is null || now - IssueWindowStartedAt >= IssueWindow)
        {
            IssueWindowStartedAt = now;
            IssuesInWindow = 0;
        }

        IssuesInWindow++;
        CodeHash = codeHash;
        SentAt = now;
        ExpiresAt = now + lifetime;
        AttemptCount = 0;
        ConsumedAt = null;
    }

    public bool CanResend(DateTimeOffset now, TimeSpan cooldown) =>
        (ConsumedAt is not null || now - SentAt >= cooldown) && !IsIssueCapReached(now);

    private bool IsIssueCapReached(DateTimeOffset now) =>
        IssueWindowStartedAt is { } start && now - start < IssueWindow && IssuesInWindow >= MaxIssuesPerWindow;

    public Result Verify(string candidateHash, DateTimeOffset now, int maxAttempts)
    {
        if (ConsumedAt is not null || now >= ExpiresAt || AttemptCount >= maxAttempts)
        {
            return IdentityErrors.CodeExpired;
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(CodeHash), Encoding.UTF8.GetBytes(candidateHash)))
        {
            AttemptCount++;
            return AttemptCount >= maxAttempts ? IdentityErrors.CodeExpired : IdentityErrors.InvalidCode;
        }

        ConsumedAt = now;
        return Result.Success();
    }
}
