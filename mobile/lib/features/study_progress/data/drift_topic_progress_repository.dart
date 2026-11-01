import '../../../core/database/app_database.dart';
import '../../../core/sync/outbox_store.dart';
import '../domain/topic_progress.dart';
import '../domain/topic_progress_repository.dart';
import 'topic_progress_records.dart';

/// Marks in the device database; each write and its queued sync request share one transaction.
class DriftTopicProgressRepository implements TopicProgressRepository {
  DriftTopicProgressRepository(
    this._db,
    this._outbox, {
    DateTime Function()? now,
  }) : _now = now ?? DateTime.now;

  final AppDatabase _db;
  final OutboxStore _outbox;
  final DateTime Function() _now;

  @override
  Stream<Map<String, ProgressStatus>> watchAll() => _db
      .select(_db.topicProgressMarks)
      .watch()
      .map(
        (rows) => {
          for (final r in rows) r.topicId: ?ProgressStatus.fromCode(r.status),
        },
      );

  @override
  Future<void> mark(String topicId, ProgressStatus status) =>
      _db.transaction(() async {
        final micros = _now().toUtc().microsecondsSinceEpoch;
        await _db
            .into(_db.topicProgressMarks)
            .insertOnConflictUpdate(
              TopicProgressMarksCompanion.insert(
                topicId: topicId,
                status: status.code,
                clientModifiedAt: micros,
              ),
            );
        await _outbox.enqueue(
          entityType: topicProgressEntity,
          entityId: topicId,
          operation: 'put',
          payload: TopicProgressRecords.putPayload(status.code, micros),
        );
      });
}
