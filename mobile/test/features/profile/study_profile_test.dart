import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/profile/domain/exam_calendar.dart';
import 'package:nirengi/features/profile/domain/study_profile.dart';

void main() {
  test('next exam year flips on 1 July in Turkish time', () {
    expect(ExamCalendar.nextExamYear(DateTime.utc(2026, 6, 30, 20, 59)), 2026);
    expect(ExamCalendar.nextExamYear(DateTime.utc(2026, 6, 30, 21)), 2027);
    expect(ExamCalendar.nextExamYear(DateTime.utc(2027, 1, 15)), 2027);
  });

  test('selectable years are the next four sittings', () {
    expect(ExamCalendar.selectableYears(DateTime.utc(2026, 10, 28)), [
      2027,
      2028,
      2029,
      2030,
    ]);
  });

  test('profile JSON uses the API codes and round-trips', () {
    const profile = StudyProfile(
      grade: Grade.graduate,
      primaryScoreType: ScoreType.dil,
      secondaryScoreType: ScoreType.soz,
      ydtLanguage: YdtLanguage.german,
      targetExamYear: 2027,
      weeklyQuestionGoal: 400,
    );

    final json = profile.toJson();
    expect(json, {
      'grade': 'Graduate',
      'primaryScoreType': 'DIL',
      'secondaryScoreType': 'SOZ',
      'ydtLanguage': 'German',
      'targetExamYear': 2027,
      'weeklyQuestionGoal': 400,
    });
    final back = StudyProfile.fromJson(json);
    expect(back.scoreTypes, [ScoreType.dil, ScoreType.soz]);
    expect(back.studiesLanguage, isTrue);
    expect(back.ydtLanguage, YdtLanguage.german);
  });
}
