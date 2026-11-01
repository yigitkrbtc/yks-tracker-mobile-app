import 'exam_schedule.dart';

/// Exam calendar API. Throws [ExamScheduleException] on failure.
abstract interface class ExamScheduleRepository {
  /// Null when [ifNoneMatch] still matches (nothing changed).
  Future<ExamSchedule?> fetch({String? ifNoneMatch});
}

abstract interface class ExamScheduleCache {
  Future<ExamSchedule?> read();

  Future<void> write(ExamSchedule schedule);
}

class ExamScheduleException implements Exception {
  const ExamScheduleException(this.message);

  final String message;

  @override
  String toString() => 'ExamScheduleException($message)';
}
