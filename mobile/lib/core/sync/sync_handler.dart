import 'package:dio/dio.dart';

import '../database/app_database.dart';
import '../network/api_problem.dart';

/// One kind of syncable record (e.g. topic tests). Features implement it in their `data` layer and register it
/// in `app/composition.dart`; core knows no feature.
abstract interface class SyncHandler {
  /// Matches [OutboxRow.entityType] for this kind of record.
  String get entityType;

  /// Key of this kind's list in the GET /v1/sync response.
  String get feedKey;

  /// Sends one queued write; returns the response body (null for 204).
  Future<Object?> push(Dio api, OutboxRow item);

  /// Inside a transaction, after the queued write was removed: store what the server answered.
  Future<void> onPushed(OutboxRow item, Object? response);

  /// Inside a transaction: the server refused the write for good (400/404).
  Future<void> onRejected(OutboxRow item, ApiProblem problem);

  /// Inside a transaction: applies records from the change feed, skipping ids in [queued] (local writes win
  /// until the server has answered them).
  Future<void> applyRemote(
    List<Map<String, dynamic>> records,
    Set<String> queued,
  );
}
