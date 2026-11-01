import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/exam_calendar/domain/exam_schedule.dart';
import 'package:nirengi/features/study_progress/domain/topic_progress.dart';

void main() {
  test('curriculum progress by count and by ÖSYM weight', () {
    final p = CurriculumProgress.of(
      [
        (id: 'a', expectedQuestions: 6.0),
        (id: 'b', expectedQuestions: 2.0),
        (id: 'c', expectedQuestions: 2.0),
        (id: 'd', expectedQuestions: 0.0),
      ],
      {
        'a': ProgressStatus.done,
        'b': ProgressStatus.inProgress,
        'd': ProgressStatus.done,
        'x': ProgressStatus.done, // outside the student's topics
      },
    );
    expect(p.done, 2);
    expect(p.inProgress, 1);
    expect(p.total, 4);
    expect(p.share, 0.5);
    expect(p.weightedShare, closeTo(0.6, 1e-9));
  });

  test('no topics is zero, not NaN', () {
    final p = CurriculumProgress.of(const [], const {});
    expect(p.share, 0);
    expect(p.weightedShare, 0);
  });

  test('status codes match the API and unknown codes read as null', () {
    expect(ProgressStatus.fromCode('InProgress'), ProgressStatus.inProgress);
    expect(ProgressStatus.fromCode('Later'), isNull);
  });

  group('Countdown', () {
    final tyt = DateTime.utc(2027, 6, 19, 7, 15); // 10:15 in Türkiye

    test('counts Türkiye calendar days', () {
      expect(Countdown.daysUntil(tyt, DateTime.utc(2027, 6, 18, 20, 59)), 1);
      expect(Countdown.daysUntil(tyt, DateTime.utc(2027, 6, 18, 21, 0)), 0);
      expect(Countdown.daysUntil(tyt, DateTime.utc(2026, 11, 1, 9)), 230);
    });

    test('schedule JSON reads the API shape', () {
      final s = ExamSchedule.fromJson({
        'version': 'v1',
        'calendars': [
          {
            'examYear': 2027,
            'sourceUrl': 'https://www.osym.gov.tr',
            'note': null,
            'events': [
              {
                'code': 'tyt',
                'name': 'TYT',
                'startsAt': '2027-06-19T10:15:00+03:00',
                'endsAt': null,
                'status': 'Estimated',
              },
            ],
          },
        ],
      });
      final tytEvent = s.year(2027)!.event('tyt')!;
      expect(tytEvent.startsAt, tyt);
      expect(tytEvent.estimated, isTrue);
      expect(
        ExamSchedule.fromJson(s.toJson()).year(2027)!.event('tyt')!.startsAt,
        tyt,
      );
    });
  });
}
