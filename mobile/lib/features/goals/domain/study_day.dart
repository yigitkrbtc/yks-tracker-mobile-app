/// Türkiye is UTC+3 all year; record dates and weeks follow it, like the API.
const turkeyOffset = Duration(hours: 3);

/// The calendar day in Türkiye at [now], as a UTC midnight (date arithmetic without DST surprises).
DateTime turkeyDay(DateTime now) {
  final local = now.toUtc().add(turkeyOffset);
  return DateTime.utc(local.year, local.month, local.day);
}

/// Monday of the week that holds [day].
DateTime weekStart(DateTime day) => DateTime.utc(
  day.year,
  day.month,
  day.day - (day.weekday - DateTime.monday),
);

/// `yyyy-MM-dd` as used by records.
DateTime parseDay(String value) {
  final d = DateTime.parse(value);
  return DateTime.utc(d.year, d.month, d.day);
}

/// Questions the student solved on one day: topic test rows plus solved mock exam sections.
class DayActivity {
  const DayActivity(this.day, this.questions);

  final DateTime day;
  final int questions;

  @override
  bool operator ==(Object other) =>
      other is DayActivity && other.day == day && other.questions == questions;

  @override
  int get hashCode => Object.hash(day, questions);

  @override
  String toString() => 'DayActivity($day, $questions)';
}
