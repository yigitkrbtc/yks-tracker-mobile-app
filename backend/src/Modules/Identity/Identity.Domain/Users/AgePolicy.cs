namespace Nirengi.Modules.Identity.Domain.Users;

/// <summary>
/// Age rules from the birth year alone. The birthday is assumed not to have passed yet this year,
/// so nobody under 13 can register and nobody under 18 is asked for marketing consent.
/// </summary>
public static class AgePolicy
{
    public const int MinimumAge = 13;

    public const int AdultAge = 18;

    public const int EarliestBirthYear = 1900;

    // Türkiye is UTC+3 all year (no DST since 2016); the calendar year flips at local midnight.
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    public static int CurrentYear(DateTimeOffset utcNow) => utcNow.ToOffset(TurkeyOffset).Year;

    public static int GuaranteedAge(int birthYear, int currentYear) => currentYear - birthYear - 1;

    public static bool CanRegister(int birthYear, int currentYear) =>
        birthYear >= EarliestBirthYear && GuaranteedAge(birthYear, currentYear) >= MinimumAge;

    public static bool IsAdult(int birthYear, int currentYear) => GuaranteedAge(birthYear, currentYear) >= AdultAge;
}
