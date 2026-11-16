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
/// How fast new codes may be requested for one user and purpose: <paramref name="Cooldown"/> between codes for the first
/// <paramref name="FreeCodes"/> codes of a <paramref name="Window"/>, then the wait doubles with every further code,
/// up to <paramref name="MaxWait"/> (ADR-0021 G4).
/// </summary>
public sealed record ResendPolicy(TimeSpan Cooldown, int FreeCodes, TimeSpan Window, TimeSpan MaxWait)
{
    public static ResendPolicy Default { get; } = new(TimeSpan.FromSeconds(60), 3, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

    /// <summary>The wait that follows the <paramref name="codesInWindow"/>-th code of the window.</summary>
    public TimeSpan WaitAfter(int codesInWindow)
    {
        var doublings = Math.Clamp(codesInWindow - FreeCodes, 0, 30);
        var wait = Cooldown * (1L << doublings);
        return wait < MaxWait ? wait : MaxWait;
    }
}

/// <summary>
/// One live 8-digit code per user and purpose; issuing a new one replaces the old. Each code allows a few attempts
/// (<c>maxAttempts</c>) and then dies. There is deliberately no account-level attempt budget: such a budget would let
/// anyone burn it with wrong guesses and lock the owner out. Instead the rate of new codes is what is limited
/// (<see cref="ResendPolicy"/>): the wait between codes grows within an hour, which never locks the owner, who can
/// still get a code by waiting, but caps how many guesses a day can buy against the 10^8 space.
/// </summary>
public sealed class VerificationCode : Entity<Guid>
{
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

    /// <summary>When the current resend window began (the first code issued after the previous window ran out).</summary>
    public DateTimeOffset WindowStartedAt { get; private set; }

    /// <summary>Codes issued in the current window, the live one included.</summary>
    public int CodesInWindow { get; private set; }

    public static VerificationCode Issue(Guid userId, CodePurpose purpose, string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        var code = new VerificationCode { Id = Guid.CreateVersion7(now), UserId = userId, Purpose = purpose };
        code.CodeHash = codeHash;
        code.SentAt = now;
        code.ExpiresAt = now + lifetime;
        code.WindowStartedAt = now;
        code.CodesInWindow = 1;
        return code;
    }

    public void Reissue(string codeHash, DateTimeOffset now, TimeSpan lifetime, ResendPolicy policy)
    {
        if (WindowExpired(now, policy))
        {
            WindowStartedAt = now;
            CodesInWindow = 1;
        }
        else
        {
            CodesInWindow++;
        }

        CodeHash = codeHash;
        SentAt = now;
        ExpiresAt = now + lifetime;
        AttemptCount = 0;
        ConsumedAt = null;
    }

    /// <summary>
    /// False while the wait is running. A consumed code does not hold the next one back: only its owner can consume it.
    /// A window that has run out starts afresh.
    /// </summary>
    public bool CanResend(DateTimeOffset now, ResendPolicy policy) =>
        ConsumedAt is not null || WindowExpired(now, policy) || now - SentAt >= policy.WaitAfter(CodesInWindow);

    private bool WindowExpired(DateTimeOffset now, ResendPolicy policy) => now - WindowStartedAt >= policy.Window;

    public Result Verify(string candidateHash, DateTimeOffset now, int maxAttempts)
    {
        if (ConsumedAt is not null || now >= ExpiresAt || AttemptCount >= maxAttempts)
        {
            return IdentityErrors.CodeExpired;
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(CodeHash), Encoding.UTF8.GetBytes(candidateHash)))
        {
            AttemptCount++;
            return AttemptCount >= maxAttempts
                ? IdentityErrors.CodeExpired
                : IdentityErrors.InvalidCode;
        }

        ConsumedAt = now;
        return Result.Success();
    }
}
