using Nirengi.Modules.Identity.Application.Abstractions;

namespace Nirengi.Modules.Identity.Application.SignIn;

public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record MfaChallenge(MfaStep Step, string MfaToken, DateTimeOffset ExpiresAt);

/// <summary>Either tokens (sign-in complete) or an MFA challenge (admin must pass TOTP first).</summary>
public sealed record SignInResult(AuthTokens? Tokens, MfaChallenge? Mfa);
