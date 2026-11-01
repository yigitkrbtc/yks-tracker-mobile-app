import 'study_day.dart';

/// The student's goal (K9): a weekly question count spread over a number of active days.
class StudyGoal {
  const StudyGoal({required this.weeklyQuestions, required this.activeDays});

  /// From the study profile; missing values take the defaults (200 questions over 5 days = 40 a day).
  factory StudyGoal.of({int? weeklyQuestions, int? activeDays}) => StudyGoal(
    weeklyQuestions: weeklyQuestions ?? defaultWeeklyQuestions,
    activeDays: (activeDays ?? defaultActiveDays).clamp(1, 7),
  );

  static const defaultWeeklyQuestions = 200;
  static const defaultActiveDays = 5;

  final int weeklyQuestions;
  final int activeDays;

  /// Questions for an active day, rounded up.
  int get dailyQuestions => (weeklyQuestions / activeDays).ceil();
}

/// Today and this week against the goal, computed on the device from local records (K10).
class GoalProgress {
  const GoalProgress({
    required this.goal,
    required this.todayQuestions,
    required this.weekQuestions,
    required this.activeDays,
    required this.weekDays,
  });

  factory GoalProgress.of(
    List<DayActivity> activity,
    StudyGoal goal,
    DateTime today,
  ) {
    final start = weekStart(today);
    var todayQuestions = 0;
    var weekQuestions = 0;
    final days = <int>{};
    for (final a in activity) {
      if (a.day.isBefore(start) || a.day.isAfter(today) || a.questions <= 0) {
        continue;
      }
      weekQuestions += a.questions;
      days.add(a.day.weekday);
      if (a.day == today) {
        todayQuestions += a.questions;
      }
    }
    return GoalProgress(
      goal: goal,
      todayQuestions: todayQuestions,
      weekQuestions: weekQuestions,
      activeDays: days.length,
      weekDays: {
        for (var d = DateTime.monday; d <= DateTime.sunday; d++)
          d: days.contains(d),
      },
    );
  }

  final StudyGoal goal;
  final int todayQuestions;
  final int weekQuestions;

  /// Days this week with at least one record.
  final int activeDays;

  /// Monday (1) … Sunday (7) → had a record.
  final Map<int, bool> weekDays;

  bool get todayMet => todayQuestions >= goal.dailyQuestions;

  bool get weekMet =>
      activeDays >= goal.activeDays && weekQuestions >= goal.weeklyQuestions;

  /// Share of today's target, 0–1.
  double get todayShare =>
      (todayQuestions / goal.dailyQuestions).clamp(0, 1).toDouble();
}

/// Consecutive weeks that met the goal (K9). The running week counts once met and never breaks the streak
/// before it ends; there is no daily streak and no freeze (the active-day allowance is the slack).
class WeekStreak {
  const WeekStreak({required this.weeks, required this.includesThisWeek});

  factory WeekStreak.of(
    List<DayActivity> activity,
    StudyGoal goal,
    DateTime today,
  ) {
    final current = weekStart(today);
    bool met(DateTime start) {
      final end = start.add(const Duration(days: 6));
      return GoalProgress.of(activity, goal, end).weekMet;
    }

    var weeks = 0;
    for (
      var start = current.subtract(const Duration(days: 7));
      met(start);
      start = start.subtract(const Duration(days: 7))
    ) {
      weeks++;
    }
    final thisWeek = GoalProgress.of(activity, goal, today).weekMet;
    return WeekStreak(
      weeks: weeks + (thisWeek ? 1 : 0),
      includesThisWeek: thisWeek,
    );
  }

  final int weeks;
  final bool includesThisWeek;
}
