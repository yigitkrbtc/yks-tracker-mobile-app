import 'package:drift/drift.dart';

import '../../../core/database/app_database.dart';
import '../domain/goal_repositories.dart';
import '../domain/study_day.dart';

/// Daily question totals straight from the record tables, so goal and streak update offline the moment a record
/// is saved (K10). Deleted records (waiting for the server) are left out.
class DriftActivityRepository implements ActivityRepository {
  DriftActivityRepository(this._db);

  final AppDatabase _db;

  @override
  Stream<List<DayActivity>> watchSince(DateTime from) {
    final since = _format(from);
    return _db
        .customSelect(
          'SELECT day, SUM(questions) AS questions FROM ('
          ' SELECT t.studied_on AS day, r.total AS questions'
          '  FROM topic_tests t JOIN topic_test_results r ON r.test_id = t.id'
          '  WHERE t.deleted = 0 AND t.studied_on >= ?1'
          ' UNION ALL'
          ' SELECT e.taken_on AS day, s.question_count AS questions'
          '  FROM mock_exams e JOIN mock_exam_sections s ON s.exam_id = e.id'
          '  WHERE e.deleted = 0 AND e.taken_on >= ?1'
          ') GROUP BY day ORDER BY day',
          variables: [Variable.withString(since)],
          readsFrom: {
            _db.topicTests,
            _db.topicTestResults,
            _db.mockExams,
            _db.mockExamSections,
          },
        )
        .watch()
        .map(
          (rows) => [
            for (final r in rows)
              DayActivity(
                parseDay(r.read<String>('day')),
                r.read<int>('questions'),
              ),
          ],
        );
  }

  static String _format(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
}
