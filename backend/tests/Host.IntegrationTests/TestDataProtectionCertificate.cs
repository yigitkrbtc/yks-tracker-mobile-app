using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Nirengi.Host.IntegrationTests;

/// <summary>A throwaway PFX so production-like hosts pass the ADR-0022 key ring certificate check.</summary>
internal static class TestDataProtectionCertificate
{
    public const string Password = "test-pfx-password";

    public static readonly Lazy<string> Base64Pfx = new(() =>
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=nirengi-dp-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, Password));
    });
}
