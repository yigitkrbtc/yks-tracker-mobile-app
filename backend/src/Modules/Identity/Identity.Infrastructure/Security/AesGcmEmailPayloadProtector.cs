using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Nirengi.Modules.Identity.Application.Abstractions;

namespace Nirengi.Modules.Identity.Infrastructure.Security;

/// <summary>
/// AES-256-GCM under a key derived (HKDF-SHA256) from <c>Identity:Security:HashKey</c>, the same secret that keys the
/// code hashes, so the payload sits behind the same trust boundary: reading the database is not enough to open it.
/// Layout (base64): version(1) | nonce(12) | tag(16) | ciphertext. The version byte is authenticated.
/// </summary>
internal sealed class AesGcmEmailPayloadProtector : IEmailPayloadProtector
{
    internal const byte Version = 1;

    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int HeaderSize = 1 + NonceSize + TagSize;

    private static readonly byte[] KeyInfo = Encoding.UTF8.GetBytes("Nirengi.Identity.EmailPayload.v1");

    private readonly byte[] _key;

    public AesGcmEmailPayloadProtector(IOptions<IdentitySecurityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Convert.FromBase64String(options.Value.HashKey), 32, salt: null, info: KeyInfo);
    }

    public string Protect(string plaintext)
    {
        var data = Encoding.UTF8.GetBytes(plaintext);
        var output = new byte[HeaderSize + data.Length];
        output[0] = Version;
        var nonce = output.AsSpan(1, NonceSize);
        var tag = output.AsSpan(1 + NonceSize, TagSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, data, output.AsSpan(HeaderSize), tag, output.AsSpan(0, 1));
        return Convert.ToBase64String(output);
    }

    /// <exception cref="CryptographicException">The value is malformed, from another key, or was altered.</exception>
    public string Unprotect(string protectedValue)
    {
        var input = new byte[(protectedValue.Length * 3 / 4) + 3];
        if (!Convert.TryFromBase64String(protectedValue, input, out var length) || length < HeaderSize)
        {
            throw new CryptographicException("The e-mail payload is malformed.");
        }

        if (input[0] != Version)
        {
            throw new CryptographicException($"The e-mail payload has unknown version {input[0]}.");
        }

        var data = new byte[length - HeaderSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(input.AsSpan(1, NonceSize), input.AsSpan(HeaderSize, length - HeaderSize), input.AsSpan(1 + NonceSize, TagSize), data, input.AsSpan(0, 1));
        return Encoding.UTF8.GetString(data);
    }
}
