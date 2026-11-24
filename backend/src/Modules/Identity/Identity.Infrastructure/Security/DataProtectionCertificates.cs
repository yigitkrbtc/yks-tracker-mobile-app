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

    public static IDataProtectionBuilder ProtectKeysWithConfiguredCertificates(this IDataProtectionBuilder builder, IConfiguration configuration)
    {
        var current = configuration[$"{Section}:Certificate"];
        if (string.IsNullOrWhiteSpace(current))
        {
            return builder;
        }

        var password = configuration[$"{Section}:CertificatePassword"];
        var certificate = Load(current, password, $"{Section}:Certificate");
        var previous = configuration.GetSection($"{Section}:PreviousCertificates").GetChildren()
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .Select(c => Load(c.Value!, password, c.Path));

        return builder
            .ProtectKeysWithCertificate(certificate)
            .UnprotectKeysWithAnyCertificate([certificate, .. previous]);
    }

    private static X509Certificate2 Load(string base64, string? password, string setting)
    {
        try
        {
            return X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(base64), password, X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            // Name the setting only; the value is a secret.
            throw new InvalidOperationException($"{setting} is not a base64 PFX that opens with DataProtection:CertificatePassword.", ex);
        }
    }
}
