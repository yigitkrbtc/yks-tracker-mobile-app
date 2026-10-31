import 'package:drift/drift.dart';

import '../../../core/database/app_database.dart';
import '../../../core/sync/outbox_store.dart';
import '../domain/topic_test.dart';
import '../domain/topic_test_repository.dart';
import 'topic_test_records.dart';

/// Topic tests in the device database. Each write and its queued sync request share one transaction.
class DriftTopicTestRepository implements TopicTestRepository {
  DriftTopicTestRepository(this._db, this._outbox, {DateTime Function()? now})
    : _now = now ?? DateTime.now;

  final AppDatabase _db;
  final OutboxStore _outbox;
  final DateTime Function() _now;

  @override
  Stream<List<TopicTest>> watchAll() {
    final t = _db.topicTests;
    final o = _db.outbox;
    final query =
        _db.select(t).join([
            leftOuterJoin(
              o,
              o.entityId.equalsExp(t.id) & o.entityType.equals(topicTestEntity),
            ),
          ])
          ..where(t.deleted.equals(false))
          ..orderBy([
            OrderingTerm.desc(t.studiedOn),
            OrderingTerm.desc(t.clientModifiedAt),
          ]);
    // Results change only together with their test row, so watching the join is enough.
    return query.watch().asyncMap((rows) async {
      final ids = [for (final r in rows) r.readTable(t).id];
      final results = await (_db.select(
        _db.topicTestResults,
      )..where((r) => r.testId.isIn(ids))).get();
      final byTest = <String, List<TopicResultRow>>{};
      for (final r in results) {
        (byTest[r.testId] ??= []).add(r);
      }
      return [
        for (final r in rows)
          TopicTestRecords.fromRows(
            r.readTable(t),
            byTest[r.readTable(t).id] ?? const [],
            r.readTableOrNull(o),
          ),
      ];
    });
  }

  @override
  Future<TopicTest?> get(String id) async {
    final row =
        await (_db.select(_db.topicTests)
              ..where((t) => t.id.equals(id) & t.deleted.equals(false)))
            .getSingleOrNull();
    if (row == null) {
      return null;
    }
    final results = await (_db.select(
      _db.topicTestResults,
    )..where((r) => r.testId.equals(id))).get();
    return TopicTestRecords.fromRows(
      row,
      results,
      await _outbox.find(topicTestEntity, id),
    );
  }

  @override
  Future<void> save(TopicTest test) => _db.transaction(() async {
    final micros = _nowMicros();
    await TopicTestRecords.writeLocal(_db, test, modifiedMicros: micros);
    await _outbox.enqueue(
      entityType: topicTestEntity,
      entityId: test.id,
      operation: 'put',
      payload: TopicTestRecords.putPayload(test, micros),
    );
  });

  /// Hides the test at once; the row goes when the server confirms. A test the server never saw still sends
  /// the delete (answered 204), which keeps a single queued write per record.
  @override
  Future<void> delete(String id) => _db.transaction(() async {
    final micros = _nowMicros();
    final changed =
        await (_db.update(_db.topicTests)..where((t) => t.id.equals(id))).write(
          TopicTestsCompanion(
            deleted: const Value(true),
            clientModifiedAt: Value(micros),
          ),
        );
    if (changed == 0) {
      return;
    }
    await _outbox.enqueue(
      entityType: topicTestEntity,
      entityId: id,
      operation: 'delete',
      payload: {'modifiedAt': TopicTestRecords.isoMicros(micros)},
    );
  });

  @override
  Future<({String? id, String? name})?> lastSource() async {
    final row =
        await (_db.select(_db.topicTests)
              ..where((t) => t.deleted.equals(false))
              ..orderBy([(t) => OrderingTerm.desc(t.clientModifiedAt)])
              ..limit(1))
            .getSingleOrNull();
    return row == null ? null : (id: row.sourceId, name: row.sourceName);
  }

  int _nowMicros() => _now().toUtc().microsecondsSinceEpoch;
}
