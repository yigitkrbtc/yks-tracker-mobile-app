using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Identity.Infrastructure.Security;
using Shouldly;

namespace Nirengi.Modules.Identity.UnitTests;

/// <summary>ADR-0022: the key ring is encrypted with a certificate from configuration; rotated-out certificates still decrypt.</summary>
public sealed class DataProtectionCertificatesTests : IDisposable
{
    private const string Password = "test-pfx-password";
    private readonly DirectoryInfo _keys = Directory.CreateTempSubdirectory("nirengi-dp-");

    public void Dispose() => _keys.Delete(recursive: true);

    [Fact]
    public void Keys_are_stored_encrypted_with_the_configured_certificate()
    {
        var protector = Protector(Settings(current: NewPfx()));

        protector.Protect("totp-secret");

        var xml = File.ReadAllText(_keys.GetFiles("key-*.xml").Single().FullName);
        xml.ShouldContain("EncryptedData");
        xml.ShouldNotContain("<masterKey");
    }

    [Fact]
    public void A_payload_protected_before_rotation_is_readable_with_the_old_certificate_listed_as_previous()
    {
        var old = NewPfx();
        var payload = Protector(Settings(current: old)).Protect("totp-secret");

        var rotated = Protector(Settings(current: NewPfx(), previous: old));

        rotated.Unprotect(payload).ShouldBe("totp-secret");
    }

    [Fact]
    public void Without_a_certificate_keys_are_not_encrypted()
    {
        Protector(Settings(current: null)).Protect("x");

        File.ReadAllText(_keys.GetFiles("key-*.xml").Single().FullName).ShouldNotContain("EncryptedData");
    }

    [Fact]
    public void A_wrong_password_fails_naming_the_setting_not_the_value()
    {
        var settings = Settings(current: NewPfx());
        settings["DataProtection:CertificatePassword"] = "wrong";

        var error = Should.Throw<InvalidOperationException>(() => Protector(settings));

        error.Message.ShouldContain("DataProtection:Certificate");
        error.Message.ShouldNotContain("wrong");
    }

    private IDataProtector Protector(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("Nirengi")
            .PersistKeysToFileSystem(_keys)
            .ProtectKeysWithConfiguredCertificates(configuration);
        return services.BuildServiceProvider().GetDataProtector("test");
    }

    private static Dictionary<string, string?> Settings(string? current, string? previous = null) => new()
    {
        ["DataProtection:Certificate"] = current,
        ["DataProtection:CertificatePassword"] = Password,
        ["DataProtection:PreviousCertificates:0"] = previous,
    };

    private static string NewPfx()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=nirengi-dp-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, Password));
    }
}
