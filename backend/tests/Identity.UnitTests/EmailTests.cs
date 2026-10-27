using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Users;
using Shouldly;

namespace Nirengi.Modules.Identity.UnitTests;

public sealed class EmailTests
{
    [Theory]
    [InlineData("İLKER@ÖRNEK.COM", "ilker@örnek.com")]
    [InlineData("  Isil@Example.com ", "isil@example.com")]
    [InlineData("IRMAK@EXAMPLE.COM", "irmak@example.com")]
    public void Normalizes_without_turkish_culture(string input, string expected)
    {
        var email = Email.Create(input).Value;

        email.Normalized.ShouldBe(expected);
        email.Value.ShouldBe(input.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("@example.com")]
    [InlineData("a@b")]
    [InlineData("a@@example.com")]
    [InlineData("a b@example.com")]
    [InlineData("a@example..com")]
    [InlineData("a@example.")]
    public void Rejects_malformed_addresses(string input)
    {
        Email.Create(input).Error.ShouldBe(IdentityErrors.InvalidEmail);
    }
}
