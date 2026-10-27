import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/auth/domain/age_policy.dart';

void main() {
  test('sign-up needs a guaranteed 13, like the API', () {
    expect(AgePolicy.latestAllowedBirthYear(2026), 2012);
    expect(AgePolicy.canRegister(2012, 2026), isTrue);
    expect(AgePolicy.canRegister(2013, 2026), isFalse);
  });

  test('adulthood needs a guaranteed 18', () {
    expect(AgePolicy.isAdult(2007, 2026), isTrue);
    expect(AgePolicy.isAdult(2008, 2026), isFalse);
  });

  test('year flips at midnight in Türkiye', () {
    expect(AgePolicy.currentYear(DateTime.utc(2026, 12, 31, 21)), 2027);
    expect(AgePolicy.currentYear(DateTime.utc(2026, 12, 31, 20, 59)), 2026);
  });
}
