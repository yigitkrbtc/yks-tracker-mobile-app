using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Nirengi.BuildingBlocks.Application.Security;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Infrastructure.Security;

/// <summary>
/// HS256 tokens. MFA tokens use their own audience, so API authentication (which only accepts the API
/// audience) can never mistake one for an access token.
/// </summary>
internal sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : ITokenIssuer
{
    private const string MfaStepClaim = "mfa_step";

    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };
    private readonly JwtOptions _options = options.Value;
    private readonly SymmetricSecurityKey _key = new(options.Value.SigningKeyBytes());

    private string MfaAudience => _options.Audience + "-mfa";

    public IssuedToken IssueAccessToken(User user, DateTimeOffset now)
    {
        var role = user.IsAdmin ? NirengiRoles.Admin : NirengiRoles.Student;
        return Issue(_options.Audience, now, _options.AccessTokenLifetime, new Dictionary<string, object>
        {
            [NirengiClaims.Subject] = user.Id.ToString(),
            [NirengiClaims.Role] = role,
        });
    }

    public IssuedToken IssueMfaToken(Guid userId, MfaStep step, DateTimeOffset now, TimeSpan lifetime) =>
        Issue(MfaAudience, now, lifetime, new Dictionary<string, object>
        {
            [NirengiClaims.Subject] = userId.ToString(),
            [MfaStepClaim] = step.ToString(),
        });

    public async Task<MfaTicket?> ReadMfaToken(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = MfaAudience,
            IssuerSigningKey = _key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
        });

        if (!result.IsValid
            || !Guid.TryParse(Claim(result.Claims, NirengiClaims.Subject), out var userId)
            || !Enum.TryParse<MfaStep>(Claim(result.Claims, MfaStepClaim), out var step))
        {
            return null;
        }

        return new MfaTicket(userId, step);
    }

    private static string? Claim(IDictionary<string, object> claims, string name) =>
        claims.TryGetValue(name, out var value) ? value as string : null;

    private IssuedToken Issue(string audience, DateTimeOffset now, TimeSpan lifetime, Dictionary<string, object> claims)
    {
        var expires = now + lifetime;
        claims[JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7(now).ToString();
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256),
        });
        return new IssuedToken(token, expires);
    }
}
