using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Abstractions;

public enum PasswordCheck
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2,
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheck Verify(string passwordHash, string password);
}

public enum MfaStep
{
    Totp = 0,
    TotpSetup = 1,
}

public sealed record IssuedToken(string Token, DateTimeOffset ExpiresAt);

public sealed record MfaTicket(Guid UserId, MfaStep Step);

public interface ITokenIssuer
{
    IssuedToken IssueAccessToken(User user, DateTimeOffset now);

    /// <summary>Short-lived token proving the password step; never accepted as an access token.</summary>
    IssuedToken IssueMfaToken(Guid userId, MfaStep step, DateTimeOffset now, TimeSpan lifetime);

    Task<MfaTicket?> ReadMfaToken(string token);
}

/// <summary>Random one-time values and their keyed hashes; only hashes are stored.</summary>
public interface IOneTimeSecrets
{
    string NewNumericCode(int digits);

    string NewOpaqueToken();

    /// <summary>Human-typeable code such as "K7QM-2XPA".</summary>
    string NewRecoveryCode();

    string Hash(string value);
}

public interface ITotpAuthenticator
{
    string NewSecret();

    string ProvisioningUri(string secret, string accountName);

    /// <summary>Returns the matched time step, or null when the code is wrong for the current window.</summary>
    long? Verify(string secret, string code, DateTimeOffset now);
}

/// <summary>Reversible encryption for secrets that must be read back (TOTP seeds).</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
