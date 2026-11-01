import 'dart:convert';

import 'package:dio/dio.dart';

import '../../../core/database/app_database.dart';
import '../../../core/network/api_problem.dart';
import '../../../core/sync/outbox_store.dart';
import '../../../core/sync/sync_handler.dart';
import 'topic_progress_records.dart';

/// Sends queued marks and applies the change feed's `topicProgress`.
class TopicProgressSyncHandler implements SyncHandler {
  TopicProgressSyncHandler(this._db, this._outbox);

  final AppDatabase _db;
  final OutboxStore _outbox;

  @override
  String get entityType => topicProgressEntity;

  @override
  String get feedKey => 'topicProgress';

  @override
  Future<Object?> push(Dio api, OutboxRow item) async {
    final response = await api.put<Map<String, dynamic>>(
      '/v1/topic-progress/${item.entityId}',
      data: jsonDecode(item.payload!),
    );
    return response.data;
  }

  /// The answer is the stored state; a newer mark from another device wins here too.
  @override
  Future<void> onPushed(OutboxRow item, Object? response) async {
    if (response is Map<String, dynamic>) {
      await TopicProgressRecords.storeServer(_db, response);
    }
  }

  /// Server code for a topic it does not know (retired from the curriculum).
  static const unknownTopic = 'tracking.unknown_topic';

  /// Only an unknown topic drops the mark on both sides. Any other refusal (validation, repeated server
  /// errors) keeps the student's mark and leaves the item rejected in the outbox, so nothing is lost silently.
  @override
  Future<void> onRejected(OutboxRow item, ApiProblem problem) async {
    if (problem.type != unknownTopic) {
      return;
    }
    final current = await _outbox.find(entityType, item.entityId);
    if (current?.revision == item.revision) {
      await _outbox.discard(entityType, item.entityId);
      await (_db.delete(
        _db.topicProgressMarks,
      )..where((m) => m.topicId.equals(item.entityId))).go();
    }
  }

  @override
  Future<void> applyRemote(
    List<Map<String, dynamic>> records,
    Set<String> queued,
  ) async {
    for (final record in records) {
      if (!queued.contains(record['topicId'])) {
        await TopicProgressRecords.storeServer(_db, record);
      }
    }
  }
}
