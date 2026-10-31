import 'dart:async';
import 'dart:convert';

import 'package:drift/drift.dart';

import '../database/app_database.dart';

/// Local writes waiting for the server. Features call [enqueue] inside the transaction that writes the
/// record, so a record and its queued write never part.
class OutboxStore {
  OutboxStore(this._db);

  final AppDatabase _db;
  final _changes = StreamController<void>.broadcast();

  /// Fires after every enqueue; the sync scheduler listens to push soon after a write.
  Stream<void> get changes => _changes.stream;

  /// Queues a write, replacing any queued write for the same record (the newest full state is what counts).
  Future<void> enqueue({
    required String entityType,
    required String entityId,
    required String operation,
    Map<String, Object?>? payload,
  }) async {
    final body = payload == null ? null : jsonEncode(payload);
    final existing = await _find(entityType, entityId);
    if (existing == null) {
      await _db
          .into(_db.outbox)
          .insert(
            OutboxCompanion.insert(
              entityType: entityType,
              entityId: entityId,
              operation: operation,
              payload: Value(body),
            ),
          );
    } else {
      await (_db.update(
        _db.outbox,
      )..where((o) => o.id.equals(existing.id))).write(
        OutboxCompanion(
          operation: Value(operation),
          payload: Value(body),
          revision: Value(existing.revision + 1),
          rejected: const Value(false),
          lastError: const Value(null),
        ),
      );
    }
    _changes.add(null);
  }

  /// Writes to push, oldest first; rejected ones wait for the student.
  Future<List<OutboxRow>> pushable() =>
      (_db.select(_db.outbox)
            ..where((o) => o.rejected.equals(false))
            ..orderBy([(o) => OrderingTerm.asc(o.id)]))
          .get();

  /// Removes [item] once pushed. False when a newer write replaced it meanwhile (that one is still queued).
  Future<bool> complete(OutboxRow item) async =>
      await (_db.delete(_db.outbox)..where(
            (o) => o.id.equals(item.id) & o.revision.equals(item.revision),
          ))
          .go() ==
      1;

  /// Parks [item] after the server refused it; a later write of the record queues it again.
  Future<void> reject(OutboxRow item, String error) =>
      (_db.update(_db.outbox)..where(
            (o) => o.id.equals(item.id) & o.revision.equals(item.revision),
          ))
          .write(
            OutboxCompanion(
              rejected: const Value(true),
              lastError: Value(error),
            ),
          );

  /// Records of [entityType] with a write still queued (rejected included); remote changes must not overwrite them.
  Future<Set<String>> queuedIds(String entityType) async {
    final rows = await (_db.select(
      _db.outbox,
    )..where((o) => o.entityType.equals(entityType))).get();
    return {for (final r in rows) r.entityId};
  }

  Future<OutboxRow?> find(String entityType, String entityId) =>
      _find(entityType, entityId);

  /// Drops a queued write (the student discarded a refused record).
  Future<void> discard(String entityType, String entityId) =>
      (_db.delete(_db.outbox)..where(
            (o) =>
                o.entityType.equals(entityType) & o.entityId.equals(entityId),
          ))
          .go();

  Stream<int> watchQueuedCount() => _countQuery().watchSingle();

  Future<int> queuedCount() => _countQuery().getSingle();

  Selectable<int> _countQuery() {
    final count = _db.outbox.id.count();
    return (_db.selectOnly(
      _db.outbox,
    )..addColumns([count])).map((r) => r.read(count) ?? 0);
  }

  Future<OutboxRow?> _find(String entityType, String entityId) =>
      (_db.select(_db.outbox)..where(
            (o) =>
                o.entityType.equals(entityType) & o.entityId.equals(entityId),
          ))
          .getSingleOrNull();

  void dispose() => _changes.close();
}
