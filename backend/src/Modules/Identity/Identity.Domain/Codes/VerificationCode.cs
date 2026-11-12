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
/// One live 6-digit code per user and purpose; issuing a new one replaces the old. Each code allows a few attempts,
/// so fresh codes could be used to keep guessing: failed attempts are therefore also counted across codes, at most
/// <see cref="MaxFailedAttemptsPerWindow"/> per <see cref="AttemptWindow"/> (25 guesses a day out of a million).
/// Issuing is never capped, so someone else asking for codes cannot keep the owner from getting one; once the budget
/// is spent, verification fails until the window ends, whichever code is live.
/// </summary>
public sealed class VerificationCode : Entity<Guid>
{
    public const int MaxFailedAttemptsPerWindow = 25;

    public static readonly TimeSpan AttemptWindow = TimeSpan.FromHours(24);

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

    /// <summary>Start of the current attempt window; null until the first failed attempt.</summary>
    public DateTimeOffset? AttemptWindowStartedAt { get; private set; }

    public int FailedAttemptsInWindow { get; private set; }

    public static VerificationCode Issue(Guid userId, CodePurpose purpose, string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        var code = new VerificationCode { Id = Guid.CreateVersion7(now), UserId = userId, Purpose = purpose };
        code.Reissue(codeHash, now, lifetime);
        return code;
    }

    public void Reissue(string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        // The attempt budget is deliberately kept: asking for a new code does not buy new guesses.
        CodeHash = codeHash;
        SentAt = now;
        ExpiresAt = now + lifetime;
        AttemptCount = 0;
        ConsumedAt = null;
    }

    public bool CanResend(DateTimeOffset now, TimeSpan cooldown) => ConsumedAt is not null || now - SentAt >= cooldown;

    public Result Verify(string candidateHash, DateTimeOffset now, int maxAttempts)
    {
        if (AttemptWindowStartedAt is { } start && now - start >= AttemptWindow)
        {
            AttemptWindowStartedAt = null;
            FailedAttemptsInWindow = 0;
        }

        if (ConsumedAt is not null || now >= ExpiresAt || AttemptCount >= maxAttempts || FailedAttemptsInWindow >= MaxFailedAttemptsPerWindow)
        {
            return IdentityErrors.CodeExpired;
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(CodeHash), Encoding.UTF8.GetBytes(candidateHash)))
        {
            AttemptWindowStartedAt ??= now;
            FailedAttemptsInWindow++;
            AttemptCount++;
            return AttemptCount >= maxAttempts || FailedAttemptsInWindow >= MaxFailedAttemptsPerWindow
                ? IdentityErrors.CodeExpired
                : IdentityErrors.InvalidCode;
        }

        ConsumedAt = now;
        return Result.Success();
    }
}
