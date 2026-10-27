/// Mirrors the API's age rules (birth year only, birthday assumed not yet passed):
/// sign-up from a guaranteed 13, adult features from a guaranteed 18.
abstract final class AgePolicy {
  static const minimumAge = 13;
  static const adultAge = 18;
  static const earliestBirthYear = 1940;

  /// Calendar year in Türkiye (UTC+3, no DST).
  static int currentYear(DateTime now) =>
      now.toUtc().add(const Duration(hours: 3)).year;

  static int latestAllowedBirthYear(int currentYear) =>
      currentYear - minimumAge - 1;

  static bool canRegister(int birthYear, int currentYear) =>
      birthYear <= latestAllowedBirthYear(currentYear);

  static bool isAdult(int birthYear, int currentYear) =>
      currentYear - birthYear - 1 >= adultAge;
}
