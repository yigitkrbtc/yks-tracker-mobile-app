import 'study_day.dart';

/// Device-only reminder choices (K11); off until the student turns them on.
class ReminderSettings {
  const ReminderSettings({
    this.daily = false,
    this.hour = 20,
    this.minute = 0,
    this.weeklyPlan = false,
  });

  factory ReminderSettings.fromJson(Map<String, dynamic> json) =>
      ReminderSettings(
        daily: json['daily'] as bool? ?? false,
        hour: json['hour'] as int? ?? 20,
        minute: json['minute'] as int? ?? 0,
        weeklyPlan: json['weeklyPlan'] as bool? ?? false,
      );

  final bool daily;
  final int hour;
  final int minute;

  /// Monday 09:00: "this week's plan is ready".
  final bool weeklyPlan;

  bool get any => daily || weeklyPlan;

  ReminderSettings copyWith({
    bool? daily,
    int? hour,
    int? minute,
    bool? weeklyPlan,
  }) => ReminderSettings(
    daily: daily ?? this.daily,
    hour: hour ?? this.hour,
    minute: minute ?? this.minute,
    weeklyPlan: weeklyPlan ?? this.weeklyPlan,
  );

  Map<String, Object?> toJson() => {
    'daily': daily,
    'hour': hour,
    'minute': minute,
    'weeklyPlan': weeklyPlan,
  };

  @override
  bool operator ==(Object other) =>
      other is ReminderSettings &&
      other.daily == daily &&
      other.hour == hour &&
      other.minute == minute &&
      other.weeklyPlan == weeklyPlan;

  @override
  int get hashCode => Object.hash(daily, hour, minute, weeklyPlan);
}

/// A reminder to schedule, at a UTC instant.
typedef PlannedReminder = ({int id, DateTime at, String title, String body});

/// Which reminders to schedule now (one-shot, so a met day can be skipped). Rebuilt on app start and after
/// every record, so the window never runs dry while the app is in use.
abstract final class ReminderPlanner {
  static const days = 7;
  static const dailyBaseId = 100;
  static const weeklyBaseId = 200;

  static List<PlannedReminder> plan({
    required ReminderSettings settings,
    required DateTime now,
    required bool todayMet,
    required int dailyQuestions,
  }) {
    final today = turkeyDay(now);
    final result = <PlannedReminder>[];
    if (settings.daily) {
      for (var i = 0; i < days; i++) {
        final day = today.add(Duration(days: i));
        final at = _instant(day, settings.hour, settings.minute);
        if (!at.isAfter(now.toUtc()) || (i == 0 && todayMet)) {
          continue;
        }
        result.add((
          id: dailyBaseId + i,
          at: at,
          title: 'Bugünün hedefi',
          body:
              'Bugün için hedefin $dailyQuestions soru. Kısa bir test bile seriyi korur.',
        ));
      }
    }
    if (settings.weeklyPlan) {
      // The next two Mondays at 09:00 still ahead.
      var monday = weekStart(today);
      var id = weeklyBaseId;
      while (id < weeklyBaseId + 2) {
        final at = _instant(monday, 9, 0);
        if (at.isAfter(now.toUtc())) {
          result.add((
            id: id++,
            at: at,
            title: 'Bu haftanın planı hazır',
            body:
                'Analizine göre bu hafta odaklanacağın konular seni bekliyor.',
          ));
        }
        monday = monday.add(const Duration(days: 7));
      }
    }
    return result;
  }

  /// [day] (a Türkiye date) at hour:minute Türkiye time, as UTC.
  static DateTime _instant(DateTime day, int hour, int minute) => DateTime.utc(
    day.year,
    day.month,
    day.day,
    hour,
    minute,
  ).subtract(turkeyOffset);
}
