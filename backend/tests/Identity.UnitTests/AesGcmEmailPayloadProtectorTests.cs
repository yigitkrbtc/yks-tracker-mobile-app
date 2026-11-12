using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Infrastructure.Security;
using Shouldly;

namespace Nirengi.Identity.UnitTests;

public sealed class AesGcmEmailPayloadProtectorTests
{
    private static readonly Guid EventId = Guid.Parse("0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");
    private static readonly DateTimeOffset ValidUntil = new(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);
    private static readonly string Aad = IdentityEmailRequested.AssociatedData(EventId, ValidUntil);

    private static AesGcmEmailPayloadProtector Create(byte[]? key = null) => new(Options.Create(new IdentitySecurityOptions
    {
        HashKey = Convert.ToBase64String(key ?? new byte[32]),
    }));

    [Fact]
    public void A_payload_round_trips_and_leaves_nothing_readable()
    {
        var protector = Create();
        const string plaintext = """{"To":"ayse@ornek.test","Body":"Kodun 123456"}""";

        var stored = protector.Protect(plaintext, Aad);

        stored.ShouldNotContain("ayse");
        stored.ShouldNotContain("123456");
        protector.Unprotect(stored, Aad).ShouldBe(plaintext);
    }

    [Fact]
    public void Every_protection_uses_a_fresh_nonce() =>
        Create().Protect("same", Aad).ShouldNotBe(Create().Protect("same", Aad));

    [Fact]
    public void A_payload_does_not_open_under_another_hash_key()
    {
        var stored = Create(RandomNumberGenerator.GetBytes(32)).Protect("secret", Aad);

        Should.Throw<CryptographicException>(() => Create(RandomNumberGenerator.GetBytes(32)).Unprotect(stored, Aad));
    }

    [Fact]
    public void A_payload_copied_under_another_event_id_does_not_open()
    {
        var protector = Create();
        var stored = protector.Protect("secret", Aad);

        Should.Throw<CryptographicException>(() =>
            protector.Unprotect(stored, IdentityEmailRequested.AssociatedData(Guid.CreateVersion7(), ValidUntil)));
    }

    [Fact]
    public void A_payload_does_not_open_with_a_later_deadline()
    {
        var protector = Create();
        var stored = protector.Protect("secret", Aad);

        Should.Throw<CryptographicException>(() =>
            protector.Unprotect(stored, IdentityEmailRequested.AssociatedData(EventId, ValidUntil.AddTicks(1))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(29)]
    public void A_flipped_byte_anywhere_is_rejected(int index)
    {
        var protector = Create();
        var raw = Convert.FromBase64String(protector.Protect("secret code 123456", Aad));
        raw[index] ^= 0x01;

        Should.Throw<CryptographicException>(() => protector.Unprotect(Convert.ToBase64String(raw), Aad));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64 !!")]
    [InlineData("AQID")]
    public void Malformed_input_is_a_cryptographic_failure_like_any_other(string value) =>
        Should.Throw<CryptographicException>(() => Create().Unprotect(value, Aad));
}
