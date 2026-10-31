import 'dart:async';
import 'dart:math';

import 'package:dio/dio.dart';

import '../database/app_database.dart';
import '../network/api_problem.dart';
import 'outbox_store.dart';
import 'sync_handler.dart';

enum SyncOutcome {
  /// Everything queued was pushed and the change feed is caught up.
  done,

  /// The server could not be reached or was busy; a retry is scheduled.
  offline,

  /// Skipped because a retry is not due yet.
  deferred,
}

/// Push local writes, then pull the change feed (ADR-0005). One run at a time; transient failures back off
/// exponentially (2 s doubling, 5 min cap), refused writes are parked and the rest continue.
class SyncService {
  SyncService(
    this._db,
    this._outbox,
    this._api,
    List<SyncHandler> handlers, {
    DateTime Function()? now,
  }) : _handlers = {for (final h in handlers) h.entityType: h},
       _now = now ?? DateTime.now;

  static const feedStream = 'main';
  static const pageSize = 500;
  static const _firstRetry = Duration(seconds: 2);
  static const _maxRetry = Duration(minutes: 5);

  final AppDatabase _db;
  final OutboxStore _outbox;
  final Dio _api;
  final Map<String, SyncHandler> _handlers;
  final DateTime Function() _now;

  Future<SyncOutcome>? _running;
  int _failures = 0;
  DateTime? _retryAt;

  /// When the next attempt is due after a failure; null when the last run succeeded.
  DateTime? get retryAt => _retryAt;

  /// Runs a sync, or joins the one in progress. [force] ignores the backoff (pull-to-refresh).
  Future<SyncOutcome> sync({bool force = false}) =>
      _running ??= _run(force).whenComplete(() => _running = null);

  /// Forgets backoff state (sign-out).
  void reset() {
    _failures = 0;
    _retryAt = null;
  }

  Future<SyncOutcome> _run(bool force) async {
    final retryAt = _retryAt;
    if (!force && retryAt != null && _now().isBefore(retryAt)) {
      return SyncOutcome.deferred;
    }
    try {
      await _push();
      await _pull();
      reset();
      return SyncOutcome.done;
    } on _Transient {
      _failures++;
      final delay = _firstRetry * pow(2, _failures - 1).toInt();
      _retryAt = _now().add(delay > _maxRetry ? _maxRetry : delay);
      return SyncOutcome.offline;
    }
  }

  Future<void> _push() async {
    for (final item in await _outbox.pushable()) {
      final handler = _handlers[item.entityType];
      if (handler == null) {
        continue; // written by a newer app version's feature; left for it
      }
      final Object? response;
      try {
        response = await handler.push(_api, item);
      } on DioException catch (e) {
        final problem = ApiProblem.fromDio(e);
        if (!_isRefusal(problem.status)) {
          throw const _Transient();
        }
        await _db.transaction(() async {
          await _outbox.reject(item, _message(problem));
          await handler.onRejected(item, problem);
        });
        continue;
      }
      await _db.transaction(() async {
        if (await _outbox.complete(item)) {
          await handler.onPushed(item, response);
        }
      });
    }
  }

  Future<void> _pull() async {
    var cursor = await _cursor();
    while (true) {
      final Map<String, dynamic> body;
      try {
        final response = await _api.get<Map<String, dynamic>>(
          '/v1/sync',
          queryParameters: {'since': cursor, 'limit': pageSize},
        );
        body = response.data!;
      } on DioException {
        throw const _Transient();
      }
      final next = body['cursor'] as int;
      await _db.transaction(() async {
        for (final handler in _handlers.values) {
          final records = (body[handler.feedKey] as List<dynamic>? ?? [])
              .cast<Map<String, dynamic>>();
          if (records.isNotEmpty) {
            await handler.applyRemote(
              records,
              await _outbox.queuedIds(handler.entityType),
            );
          }
        }
        await _db
            .into(_db.syncCursors)
            .insertOnConflictUpdate(
              SyncCursorsCompanion.insert(stream: feedStream, cursor: next),
            );
      });
      if (body['hasMore'] != true || next == cursor) {
        return;
      }
      cursor = next;
    }
  }

  Future<int> _cursor() async {
    final row = await (_db.select(
      _db.syncCursors,
    )..where((c) => c.stream.equals(feedStream))).getSingleOrNull();
    return row?.cursor ?? 0;
  }

  /// The server looked at the write and said no; sending it again would not help.
  static bool _isRefusal(int status) =>
      status == 400 || status == 404 || status == 422;

  static String _message(ApiProblem problem) {
    final fields = problem.fieldErrors.values.expand((m) => m);
    return fields.isNotEmpty
        ? fields.join('\n')
        : problem.title ?? 'Sunucu kaydı kabul etmedi.';
  }
}

class _Transient implements Exception {
  const _Transient();
}
