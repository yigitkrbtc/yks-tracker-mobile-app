/// YKS is held every June; from July on, the next sitting is the following year's. Mirrors the API rule.
abstract final class ExamCalendar {
  /// A 9th grader's regular exam is three sittings after the next one.
  static const maxYearsAhead = 3;

  // Türkiye is UTC+3 all year.
  static const _turkeyOffset = Duration(hours: 3);

  static int nextExamYear(DateTime now) {
    final local = now.toUtc().add(_turkeyOffset);
    return local.month <= 6 ? local.year : local.year + 1;
  }

  /// Years the API accepts as a target, nearest first.
  static List<int> selectableYears(DateTime now) {
    final next = nextExamYear(now);
    return [for (var i = 0; i <= maxYearsAhead; i++) next + i];
  }
}
