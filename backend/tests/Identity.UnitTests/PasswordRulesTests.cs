using Nirengi.Modules.Identity.Application;
using Shouldly;

namespace Nirengi.Modules.Identity.UnitTests;

public sealed class PasswordRulesTests
{
    [Theory]
    [InlineData("password123")]
    [InlineData("Sifre12345")]
    [InlineData("ŞİFRE2024!")]
    [InlineData("Galatasaray1905!")]
    [InlineData("qwertyuiop12")]
    [InlineData("1234567890")]
    [InlineData("aaaaaaaa11")]
    [InlineData("P@ssw0rd1")]
    [InlineData("P@$$w0rd2024")]
    [InlineData("Passw0rd!")]
    [InlineData("S1fr3-2024")]
    [InlineData("4dm1n12345")]
    public void Common_passwords_are_refused(string password) =>
        PasswordRules.IsCommon(password).ShouldBeTrue();

    [Theory]
    [InlineData("dogruSifre123")]
    [InlineData("MaviKedi47Uçar")]
    [InlineData("kirmizi-Balon-88")]
    [InlineData("Nirengi'de yks 2027 hazirlik")]
    [InlineData("Sifre!2024abc")]
    [InlineData("tr0ub4dor&3x")]
    public void Ordinary_passwords_are_accepted(string password) =>
        PasswordRules.IsCommon(password).ShouldBeFalse();

    [Fact]
    public void Own_name_plus_digits_is_refused()
    {
        PasswordRules.IsCommon("Zeynep1905", ["Zeynep", "Kaya"]).ShouldBeTrue();
        PasswordRules.IsCommon("kaya_2008", ["Zeynep", "Kaya"]).ShouldBeTrue();
        PasswordRules.IsCommon("Z3ynep2008", ["Zeynep", "Kaya"]).ShouldBeTrue();
        PasswordRules.IsCommon("ÖmerFarukSelçuk", ["Ömer Faruk", "Selçuk"]).ShouldBeFalse();
    }

    [Fact]
    public void Empty_password_is_left_to_the_length_rule() =>
        PasswordRules.IsCommon("").ShouldBeFalse();
}
