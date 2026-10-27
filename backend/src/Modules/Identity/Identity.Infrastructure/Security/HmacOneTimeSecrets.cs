using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nirengi.Modules.Identity.Application.Abstractions;

namespace Nirengi.Modules.Identity.Infrastructure.Security;

internal sealed class HmacOneTimeSecrets(IOptions<IdentitySecurityOptions> options) : IOneTimeSecrets
{
    // Crockford base32 without I, L, O, U: nothing a person can misread when typing a recovery code.
    private const string RecoveryAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private readonly byte[] _key = Convert.FromBase64String(options.Value.HashKey);

    public string NewNumericCode(int digits) =>
        string.Create(digits, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
            }
        });

    public string NewOpaqueToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public string NewRecoveryCode()
    {
        var raw = RandomNumberGenerator.GetString(RecoveryAlphabet, 8);
        return $"{raw[..4]}-{raw[4..]}";
    }

    public string Hash(string value) => Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value)));
}
