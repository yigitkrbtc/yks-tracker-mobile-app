using Nirengi.Modules.Identity.Domain.Users;
using Shouldly;

namespace Nirengi.Modules.Identity.UnitTests;

public sealed class AgePolicyTests
{
    [Theory]
    [InlineData(2012, true)]
    [InlineData(2013, false)]
    [InlineData(1899, false)]
    public void Registration_requires_a_guaranteed_13(int birthYear, bool expected) =>
        AgePolicy.CanRegister(birthYear, 2026).ShouldBe(expected);

    [Theory]
    [InlineData(2007, true)]
    [InlineData(2008, false)]
    public void Adulthood_requires_a_guaranteed_18(int birthYear, bool expected) =>
        AgePolicy.IsAdult(birthYear, 2026).ShouldBe(expected);

    [Fact]
    public void Year_flips_at_turkish_midnight()
    {
        AgePolicy.CurrentYear(new DateTimeOffset(2026, 12, 31, 21, 0, 0, TimeSpan.Zero)).ShouldBe(2027);
        AgePolicy.CurrentYear(new DateTimeOffset(2026, 12, 31, 20, 59, 0, TimeSpan.Zero)).ShouldBe(2026);
    }
}
