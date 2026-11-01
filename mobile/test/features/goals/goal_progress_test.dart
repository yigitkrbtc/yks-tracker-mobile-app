import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/goals/domain/goal_progress.dart';
import 'package:nirengi/features/goals/domain/reminders.dart';
import 'package:nirengi/features/goals/domain/study_day.dart';

DateTime d(int month, int day) => DateTime.utc(2026, month, day);

void main() {
  // Wednesday 4 November 2026; the week starts on Monday 2 November.
  final today = d(11, 4);
  const goal = StudyGoal(weeklyQuestions: 100, activeDays: 3);

  group('study day', () {
    test('Türkiye day changes at 21:00 UTC', () {
      expect(turkeyDay(DateTime.utc(2026, 11, 1, 20, 59)), d(11, 1));
      expect(turkeyDay(DateTime.utc(2026, 11, 1, 21, 0)), d(11, 2));
    });

    test('weeks start on Monday', () {
      expect(weekStart(d(11, 8)), d(11, 2)); // Sunday
      expect(weekStart(d(11, 9)), d(11, 9)); // Monday
    });
  });

  group('StudyGoal', () {
    test('defaults to 200 questions over 5 days, 40 a day', () {
      final g = StudyGoal.of();
      expect(g.weeklyQuestions, 200);
      expect(g.activeDays, 5);
      expect(g.dailyQuestions, 40);
    });

    test('a day\'s share rounds up', () {
      expect(goal.dailyQuestions, 34);
      expect(StudyGoal.of(weeklyQuestions: 50, activeDays: 9).activeDays, 7);
    });
  });

  group('GoalProgress', () {
    test('counts this week only and today separately', () {
      final p = GoalProgress.of(
        [
          DayActivity(d(11, 1), 80), // last Sunday
          DayActivity(d(11, 2), 30),
          DayActivity(d(11, 4), 40),
          DayActivity(d(11, 5), 50), // tomorrow (clock skew): ignored
        ],
        goal,
        today,
      );

      expect(p.todayQuestions, 40);
      expect(p.weekQuestions, 70);
      expect(p.activeDays, 2);
      expect(p.weekDays[DateTime.monday], isTrue);
      expect(p.weekDays[DateTime.tuesday], isFalse);
      expect(p.todayMet, isTrue);
      expect(p.weekMet, isFalse);
    });

    test('the week is met with enough days and questions', () {
      final p = GoalProgress.of(
        [
          DayActivity(d(11, 2), 30),
          DayActivity(d(11, 3), 30),
          DayActivity(d(11, 4), 40),
        ],
        goal,
        today,
      );
      expect(p.weekMet, isTrue);
      expect(p.todayShare, 1);
    });

    test('a day without questions is not active', () {
      final p = GoalProgress.of([DayActivity(d(11, 3), 0)], goal, today);
      expect(p.activeDays, 0);
    });
  });

  group('WeekStreak', () {
    List<DayActivity> metWeek(DateTime monday) => [
      for (var i = 0; i < 3; i++)
        DayActivity(monday.add(Duration(days: i)), 34),
    ];

    test('counts consecutive met weeks before this one', () {
      final s = WeekStreak.of(
        [
          ...metWeek(d(10, 5)),
          // 12 October week missed: breaks the older run
          ...metWeek(d(10, 19)),
          ...metWeek(d(10, 26)),
        ],
        goal,
        today,
      );
      expect(s.weeks, 2);
      expect(s.includesThisWeek, isFalse);
    });

    test(
      'the running week adds one once met and never breaks the streak early',
      () {
        final history = [...metWeek(d(10, 26))];
        expect(WeekStreak.of(history, goal, today).weeks, 1);
        expect(
          WeekStreak.of([...history, ...metWeek(d(11, 2))], goal, today).weeks,
          2,
        );
      },
    );

    test('a missed last week resets to zero', () {
      expect(WeekStreak.of(metWeek(d(10, 19)), goal, today).weeks, 0);
    });

    test('Sunday night still belongs to the week', () {
      final sunday = d(11, 8);
      final s = WeekStreak.of(
        [
          DayActivity(d(11, 2), 40),
          DayActivity(d(11, 6), 30),
          DayActivity(sunday, 30),
        ],
        goal,
        sunday,
      );
      expect(s.includesThisWeek, isTrue);
      // Monday: last week is now history.
      expect(
        WeekStreak.of(
          [
            DayActivity(d(11, 2), 40),
            DayActivity(d(11, 6), 30),
            DayActivity(sunday, 30),
          ],
          goal,
          d(11, 9),
        ).weeks,
        1,
      );
    });
  });

  group('ReminderPlanner', () {
    // 18:00 in Türkiye on Wednesday.
    final now = DateTime.utc(2026, 11, 4, 15);

    test('nothing when reminders are off', () {
      expect(
        ReminderPlanner.plan(
          settings: const ReminderSettings(),
          now: now,
          todayMet: false,
          dailyQuestions: 40,
        ),
        isEmpty,
      );
    });

    test(
      'daily reminders for the next seven days at the chosen Türkiye time',
      () {
        final plan = ReminderPlanner.plan(
          settings: const ReminderSettings(daily: true, hour: 20, minute: 30),
          now: now,
          todayMet: false,
          dailyQuestions: 40,
        );
        expect(plan, hasLength(7));
        expect(plan.first.at, DateTime.utc(2026, 11, 4, 17, 30));
        expect(plan.first.id, ReminderPlanner.dailyBaseId);
        expect(plan.first.body, contains('40 soru'));
        expect(plan.last.at, DateTime.utc(2026, 11, 10, 17, 30));
      },
    );

    test('a met day and a passed time skip today', () {
      final met = ReminderPlanner.plan(
        settings: const ReminderSettings(daily: true, hour: 20),
        now: now,
        todayMet: true,
        dailyQuestions: 40,
      );
      expect(met, hasLength(6));
      expect(met.first.at, DateTime.utc(2026, 11, 5, 17));

      final late = ReminderPlanner.plan(
        settings: const ReminderSettings(daily: true, hour: 9),
        now: now,
        todayMet: false,
        dailyQuestions: 40,
      );
      expect(late.first.at, DateTime.utc(2026, 11, 5, 6));
    });

    test('weekly plan reminder on the next two Mondays at 09:00', () {
      final plan = ReminderPlanner.plan(
        settings: const ReminderSettings(weeklyPlan: true),
        now: now,
        todayMet: false,
        dailyQuestions: 40,
      );
      expect(plan.map((r) => r.at), [
        DateTime.utc(2026, 11, 9, 6),
        DateTime.utc(2026, 11, 16, 6),
      ]);
      expect(plan.map((r) => r.id), [200, 201]);
    });

    test('settings round-trip through JSON', () {
      const s = ReminderSettings(
        daily: true,
        hour: 7,
        minute: 15,
        weeklyPlan: true,
      );
      expect(ReminderSettings.fromJson(s.toJson()), s);
    });
  });
}
