using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Domain.Tokens;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.SignIn;

/// <summary>Final step of every sign-in path. Callers persist the changes.</summary>
internal sealed class SignInService(
    ITokenIssuer tokens,
    IOneTimeSecrets secrets,
    IRefreshTokenRepository refreshTokens,
    IdentityOptions options)
{
    public SignInResult Complete(User user, DateTimeOffset now)
    {
        if (!user.IsAdmin)
        {
            return new SignInResult(IssueTokens(user, now), null);
        }

        var step = user.TotpEnabled ? MfaStep.Totp : MfaStep.TotpSetup;
        var mfa = tokens.IssueMfaToken(user.Id, step, now, options.MfaTokenLifetime);
        return new SignInResult(null, new MfaChallenge(step, mfa.Token, mfa.ExpiresAt));
    }

    public AuthTokens IssueTokens(User user, DateTimeOffset now)
    {
        var raw = secrets.NewOpaqueToken();
        var refresh = RefreshToken.Issue(user.Id, secrets.Hash(raw), now, options.RefreshTokenLifetime);
        refreshTokens.Add(refresh);
        return Tokens(user, refresh, raw, now);
    }

    public AuthTokens Rotate(User user, RefreshToken current, DateTimeOffset now)
    {
        var raw = secrets.NewOpaqueToken();
        var next = current.Rotate(secrets.Hash(raw), now);
        refreshTokens.Add(next);
        return Tokens(user, next, raw, now);
    }

    public async Task RevokeAll(Guid userId, RevokeReason reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in await refreshTokens.GetUnrevokedByUser(userId, cancellationToken))
        {
            token.Revoke(now, reason);
        }
    }

    public async Task RevokeFamily(Guid familyId, RevokeReason reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in await refreshTokens.GetUnrevokedByFamily(familyId, cancellationToken))
        {
            token.Revoke(now, reason);
        }
    }

    private AuthTokens Tokens(User user, RefreshToken refresh, string rawRefresh, DateTimeOffset now)
    {
        var access = tokens.IssueAccessToken(user, now);
        return new AuthTokens(access.Token, access.ExpiresAt, rawRefresh, refresh.ExpiresAt);
    }
}
