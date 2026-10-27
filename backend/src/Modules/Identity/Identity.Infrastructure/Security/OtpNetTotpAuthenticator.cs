using Microsoft.Extensions.Options;
using Nirengi.Modules.Identity.Application.Abstractions;
using OtpNet;

namespace Nirengi.Modules.Identity.Infrastructure.Security;

/// <summary>RFC 6238: SHA-1, 6 digits, 30 s steps, one step of drift either way.</summary>
internal sealed class OtpNetTotpAuthenticator(IOptions<IdentitySecurityOptions> options) : ITotpAuthenticator
{
    private static readonly VerificationWindow Window = new(previous: 1, future: 1);

    public string NewSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public string ProvisioningUri(string secret, string accountName) =>
        new OtpUri(OtpType.Totp, secret, accountName, options.Value.TotpIssuer).ToString();

    public long? Verify(string secret, string code, DateTimeOffset now) =>
        new Totp(Base32Encoding.ToBytes(secret)).VerifyTotp(now.UtcDateTime, code, out var step, Window) ? step : null;
}
