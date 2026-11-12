using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Nirengi.Modules.Identity.Infrastructure.Security;
using Shouldly;

namespace Nirengi.Identity.UnitTests;

public sealed class AesGcmEmailPayloadProtectorTests
{
    private static AesGcmEmailPayloadProtector Create(byte[]? key = null) => new(Options.Create(new IdentitySecurityOptions
    {
        HashKey = Convert.ToBase64String(key ?? new byte[32]),
    }));

    [Fact]
    public void A_payload_round_trips_and_leaves_nothing_readable()
    {
        var protector = Create();
        const string plaintext = """{"To":"ayse@ornek.test","Body":"Kodun 123456"}""";

        var stored = protector.Protect(plaintext);

        stored.ShouldNotContain("ayse");
        stored.ShouldNotContain("123456");
        protector.Unprotect(stored).ShouldBe(plaintext);
    }

    [Fact]
    public void Every_protection_uses_a_fresh_nonce() =>
        Create().Protect("same").ShouldNotBe(Create().Protect("same"));

    [Fact]
    public void A_payload_does_not_open_under_another_hash_key()
    {
        var stored = Create(RandomNumberGenerator.GetBytes(32)).Protect("secret");

        Should.Throw<CryptographicException>(() => Create(RandomNumberGenerator.GetBytes(32)).Unprotect(stored));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(29)]
    public void A_flipped_byte_anywhere_is_rejected(int index)
    {
        var protector = Create();
        var raw = Convert.FromBase64String(protector.Protect("secret code 123456"));
        raw[index] ^= 0x01;

        Should.Throw<CryptographicException>(() => protector.Unprotect(Convert.ToBase64String(raw)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64 !!")]
    [InlineData("AQID")]
    public void Malformed_input_is_a_cryptographic_failure_like_any_other(string value) =>
        Should.Throw<CryptographicException>(() => Create().Unprotect(value));
}
