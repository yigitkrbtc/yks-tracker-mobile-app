import 'dart:convert';

import 'package:dio/dio.dart';

import '../../../core/database/app_database.dart';
import '../../../core/network/api_problem.dart';
import '../../../core/sync/outbox_store.dart';
import '../../../core/sync/sync_handler.dart';
import 'mock_exam_records.dart';

/// Sends queued mock exam writes and applies the change feed's `mockExams`.
class MockExamSyncHandler implements SyncHandler {
  MockExamSyncHandler(this._db, this._outbox);

  final AppDatabase _db;
  final OutboxStore _outbox;

  @override
  String get entityType => mockExamEntity;

  @override
  String get feedKey => 'mockExams';

  @override
  Future<Object?> push(Dio api, OutboxRow item) async {
    final path = '/v1/mock-exams/${item.entityId}';
    final payload = jsonDecode(item.payload!) as Map<String, dynamic>;
    if (item.operation == 'delete') {
      // 204 when deleted; 200 with the stored exam when a newer edit from another device wins.
      final response = await api.delete<Object?>(
        path,
        queryParameters: {'modifiedAt': payload['modifiedAt']},
      );
      return response.data;
    }
    final response = await api.put<Map<String, dynamic>>(path, data: payload);
    return response.data;
  }

  /// The server's answer is the stored state: a newer edit from another device or a delete wins here too.
  @override
  Future<void> onPushed(OutboxRow item, Object? response) async {
    if (response is Map<String, dynamic>) {
      await MockExamRecords.storeServer(_db, response);
    } else if (item.operation == 'delete') {
      await _removeRow(item.entityId);
    }
  }

  /// A refused edit stays visible with its error. A refused delete has nothing to show, so both copies go.
  @override
  Future<void> onRejected(OutboxRow item, ApiProblem problem) async {
    if (item.operation != 'delete') {
      return;
    }
    final current = await _outbox.find(entityType, item.entityId);
    if (current?.revision == item.revision) {
      await _outbox.discard(entityType, item.entityId);
      await _removeRow(item.entityId);
    }
  }

  @override
  Future<void> applyRemote(
    List<Map<String, dynamic>> records,
    Set<String> queued,
  ) async {
    for (final record in records) {
      if (!queued.contains(record['id'])) {
        await MockExamRecords.storeServer(_db, record);
      }
    }
  }

  Future<void> _removeRow(String id) =>
      (_db.delete(_db.mockExams)..where((e) => e.id.equals(id))).go();
}
