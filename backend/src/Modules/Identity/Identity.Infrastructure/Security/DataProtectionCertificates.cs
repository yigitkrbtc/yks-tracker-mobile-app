using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace Nirengi.Modules.Identity.Infrastructure.Security;

/// <summary>
/// ADR-0022: the key ring (which protects TOTP secrets) is encrypted at rest with a PFX certificate held as a secret
/// (<c>DataProtection:Certificate</c>, base64). Rotated-out certificates move to <c>DataProtection:PreviousCertificates</c>
/// and are never removed: keys they encrypted keep protecting TOTP secrets indefinitely. Without a certificate (Development)
/// keys stay unencrypted; <c>ProductionSafetyValidator</c> refuses that in production.
/// </summary>
public static class DataProtectionCertificates
{
    private const string Section = "DataProtection";
    private const int MinimumKeySize = 3072;

    public static IDataProtectionBuilder ProtectKeysWithConfiguredCertificates(this IDataProtectionBuilder builder, IConfiguration configuration)
    {
        var current = configuration[$"{Section}:Certificate"];
        var previousSettings = configuration.GetSection($"{Section}:PreviousCertificates").GetChildren()
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .ToList();
        if (string.IsNullOrWhiteSpace(current))
        {
            // Old certificates alone would leave new keys unencrypted and old ones unreadable, silently.
            return previousSettings.Count == 0
                ? builder
                : throw new InvalidOperationException($"{Section}:PreviousCertificates is set but {Section}:Certificate is empty; set the current certificate.");
        }

        var password = configuration[$"{Section}:CertificatePassword"];
        var certificate = Load(current, password, $"{Section}:Certificate");
        var previous = previousSettings.Select(c => Load(c.Value!, password, c.Path)).ToList();

        return builder
            .ProtectKeysWithCertificate(certificate)
            .UnprotectKeysWithAnyCertificate([certificate, .. previous]);
    }

    private static X509Certificate2 Load(string base64, string? password, string setting)
    {
        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(base64), password, X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            // Name the setting only; the value is a secret.
            throw new InvalidOperationException($"{setting} is not a base64 PFX that opens with DataProtection:CertificatePassword.", ex);
        }

        // Without the private key, keys written now could never be decrypted after a restart: fail before writing any.
        using var rsa = certificate.GetRSAPublicKey();
        if (!certificate.HasPrivateKey || rsa is not { KeySize: >= MinimumKeySize })
        {
            certificate.Dispose();
            throw new InvalidOperationException($"{setting} must be an RSA {MinimumKeySize}+ PFX that includes its private key.");
        }

        return certificate;
    }
}
