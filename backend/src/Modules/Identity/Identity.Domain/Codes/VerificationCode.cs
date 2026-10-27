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

/// <summary>One live 6-digit code per user and purpose; issuing a new one replaces the old.</summary>
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

    public static VerificationCode Issue(Guid userId, CodePurpose purpose, string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        var code = new VerificationCode { Id = Guid.CreateVersion7(now), UserId = userId, Purpose = purpose };
        code.Reissue(codeHash, now, lifetime);
        return code;
    }

    public void Reissue(string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        CodeHash = codeHash;
        SentAt = now;
        ExpiresAt = now + lifetime;
        AttemptCount = 0;
        ConsumedAt = null;
    }

    public bool CanResend(DateTimeOffset now, TimeSpan cooldown) => ConsumedAt is not null || now - SentAt >= cooldown;

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
