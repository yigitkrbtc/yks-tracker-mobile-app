import 'dart:async';
import 'dart:convert';
import 'dart:math';

import 'package:dio/dio.dart';
import 'package:drift/drift.dart' show Value;

import '../database/app_database.dart';
import '../network/api_problem.dart';
import 'outbox_store.dart';
import 'sync_handler.dart';

/// What to tell the student when a run ends [SyncOutcome.failed].
const syncFailedMessage =
    'Bazı kayıtlar eşitlenemedi; kayıtların bu cihazda duruyor. '
    'Sorun sürerse uygulamayı güncelle.';

enum SyncOutcome {
  /// Everything queued was pushed and the change feed is caught up.
  done,

  /// The server could not be reached, was busy, or failed on some writes; a retry is scheduled.
  offline,

  /// Something unexpected broke the run (e.g. a feed record this version cannot read), not the network.
  /// A retry is scheduled like for [offline], but it keeps failing until the app is updated.
  failed,

  /// Skipped because a retry is not due yet.
  deferred,
}

/// Push local writes, then pull the change feed (ADR-0005). One run at a time; a call during a run queues one
/// more run after it, so a write made meanwhile is not left waiting. Failures back off exponentially (2 s
/// doubling, 5 min cap). Refused writes are parked; a write the server keeps failing on is skipped so the rest
/// and the pull still go, and parked after [maxServerFailures] tries so the student can see it.
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
  static const maxServerFailures = 5;

  /// Epoch changes handled in one pull before giving up on it (two restores during one pull is already absurd).
  static const _maxEpochResets = 3;
  static const _firstRetry = Duration(seconds: 2);
  static const _maxRetry = Duration(minutes: 5);

  final AppDatabase _db;
  final OutboxStore _outbox;
  final Dio _api;
  final Map<String, SyncHandler> _handlers;
  final DateTime Function() _now;

  Future<SyncOutcome>? _running;
  bool _again = false;
  bool _againForced = false;
  int _generation = 0;
  int _failures = 0;
  DateTime? _retryAt;
  final Map<String, int> _serverFailures =
      {}; // per queued write (id:revision), this app session

  final _completed = StreamController<void>.broadcast();

  /// Fires after each run that pushed every queued write and caught up with the change feed: the server now
  /// holds everything this device knows, so read models built from it (analysis) are worth fetching again.
  Stream<void> get completed => _completed.stream;

  /// When the next attempt is due after a failure; null when the last run succeeded.
  DateTime? get retryAt => _retryAt;

  /// How long until [retryAt] on this service's clock (never negative); null when no retry is due.
  Duration? get retryIn {
    final at = _retryAt;
    if (at == null) {
      return null;
    }
    final left = at.difference(_now());
    return left.isNegative ? Duration.zero : left;
  }

  /// Runs a sync; during a run, queues one more after it and waits for both. [force] ignores the backoff
  /// (pull-to-refresh).
  Future<SyncOutcome> sync({bool force = false}) {
    final running = _running;
    if (running != null) {
      _again = true;
      _againForced |= force;
      return running;
    }
    return _running = _loop(force, _generation);
  }

  Future<void> dispose() => _completed.close();

  /// Forgets backoff state and abandons a run in progress (sign-out): it writes nothing more to the database
  /// and sends nothing more to the server.
  void reset() {
    _generation++;
    _running = null;
    _again = _againForced = false;
    _serverFailures.clear();
    _clearBackoff();
  }

  Future<SyncOutcome> _loop(bool force, int generation) async {
    try {
      var outcome = await _run(force, generation);
      while (_again && generation == _generation) {
        force = _againForced;
        _again = _againForced = false;
        outcome = await _run(force, generation);
      }
      return outcome;
    } on _Abandoned {
      return SyncOutcome.deferred;
    } finally {
      if (generation == _generation) {
        _running = null;
      }
    }
  }

  Future<SyncOutcome> _run(bool force, int generation) async {
    final retryAt = _retryAt;
    if (!force && retryAt != null && _now().isBefore(retryAt)) {
      return SyncOutcome.deferred;
    }
    var outcome = SyncOutcome.offline;
    try {
      // Writes the server failed on in this run are not tried again in it (they count once per run).
      final failed = <String>{};
      var allPushed = await _push(generation, failed);
      if (await _pull(generation)) {
        // The server's history was restored: send what it lacks, then catch up.
        allPushed = await _push(generation, failed) && allPushed;
        await _pull(generation);
      }
      if (allPushed) {
        _clearBackoff();
        if (generation == _generation) {
          _completed.add(null);
        }
        return SyncOutcome.done;
      }
    } on _Abandoned {
      rethrow;
    } on _Transient {
      // Server unreachable or busy: back off below.
    } catch (_) {
      // Unexpected (e.g. a feed record this version cannot read): back off instead of failing every run at once,
      // but report it apart from a network problem.
      if (generation != _generation) {
        throw const _Abandoned();
      }
      outcome = SyncOutcome.failed;
    }
    _failures++;
    final delay = _firstRetry * pow(2, _failures - 1).toInt();
    _retryAt = _now().add(delay > _maxRetry ? _maxRetry : delay);
    return outcome;
  }

  /// False when the server failed on some writes; they stay queued for the next run. Keys of those writes are
  /// added to [failed], and writes already in it are skipped.
  Future<bool> _push(int generation, Set<String> failed) async {
    var allPushed = true;
    for (final item in await _outbox.pushable()) {
      final handler = _handlers[item.entityType];
      if (handler == null) {
        continue; // written by a newer app version's feature; left for it
      }
      final key = '${item.id}:${item.revision}';
      if (failed.contains(key)) {
        allPushed = false;
        continue;
      }
      _ensureLive(generation);
      final Object? response;
      try {
        response = await handler.push(_api, item);
      } on DioException catch (e) {
        final problem = ApiProblem.fromDio(e);
        if (_isUnreachable(problem.status)) {
          throw const _Transient();
        }
        final failures = _isRefusal(problem.status)
            ? maxServerFailures
            : (_serverFailures[key] ?? 0) + 1;
        if (failures < maxServerFailures) {
          _serverFailures[key] = failures;
          failed.add(key);
          allPushed = false;
          continue;
        }
        _serverFailures.remove(key);
        await _transaction(generation, () async {
          await _outbox.reject(item, _message(problem));
          await handler.onRejected(item, problem);
        });
        continue;
      }
      await _transaction(generation, () async {
        if (await _outbox.complete(item)) {
          await handler.onPushed(item, response);
        }
      });
    }
    return allPushed;
  }

  /// True when the server's sync epoch changed: every local record was queued again, the cursor went back to 0
  /// and the feed was read from the start, so the caller pushes once more.
  ///
  /// While reading the restored feed, a record queued again only because of the epoch change is dropped from the
  /// queue when the server already holds it at the same or a newer edit time: only what the restore lost is sent,
  /// so a large history does not run into the per-user rate limit.
  Future<bool> _pull(int generation) async {
    final stored = await _storedCursor();
    var cursor = stored?.cursor ?? 0;
    var epoch = stored?.epoch;
    var resets = 0;
    final requeued = <String, Set<String>>{};
    while (true) {
      _ensureLive(generation);
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
      final served = body['epoch'] as String?;
      // A cursor kept without an epoch was read before epochs existed: it cannot be trusted either.
      if (served != null && served != epoch && (epoch != null || cursor > 0)) {
        if (++resets > _maxEpochResets) {
          throw StateError('Sync epoch keeps changing');
        }
        await _transaction(generation, () async {
          for (final handler in _handlers.values) {
            final before = await _outbox.queuedIds(handler.entityType);
            await handler.requeueAll(before);
            final added = (await _outbox.queuedIds(handler.entityType))
                .difference(before);
            (requeued[handler.entityType] ??= {}).addAll(added);
          }
          await _saveCursor(0, served);
        });
        epoch = served;
        cursor = 0;
        continue;
      }
      epoch = served ?? epoch;
      final next = body['cursor'] as int;
      await _transaction(generation, () async {
        for (final handler in _handlers.values) {
          final records = (body[handler.feedKey] as List<dynamic>? ?? [])
              .cast<Map<String, dynamic>>();
          if (records.isNotEmpty) {
            await _pruneHeld(handler, records, requeued[handler.entityType]);
            await handler.applyRemote(
              records,
              await _outbox.queuedIds(handler.entityType),
            );
          }
        }
        await _saveCursor(next, epoch);
      });
      if (body['hasMore'] != true || next == cursor) {
        return resets > 0;
      }
      cursor = next;
    }
  }

  /// Drops requeued writes (still untouched: revision 0) of records the server holds at the same or a newer edit
  /// time; the record then takes the server state through [SyncHandler.applyRemote].
  Future<void> _pruneHeld(
    SyncHandler handler,
    List<Map<String, dynamic>> records,
    Set<String>? requeued,
  ) async {
    if (requeued == null || requeued.isEmpty) {
      return;
    }
    for (final record in records) {
      final id = handler.recordId(record);
      if (!requeued.contains(id)) {
        continue;
      }
      final item = await _outbox.find(handler.entityType, id);
      if (item != null &&
          item.revision == 0 &&
          item.operation == 'put' &&
          !item.rejected &&
          _serverHolds(record, item) &&
          await _outbox.complete(item)) {
        requeued.remove(id);
      }
    }
  }

  static bool _serverHolds(Map<String, dynamic> record, OutboxRow item) {
    final server = DateTime.tryParse(
      record['clientModifiedAt'] as String? ?? '',
    );
    final payload = item.payload == null ? null : jsonDecode(item.payload!);
    final local = payload is Map<String, dynamic>
        ? DateTime.tryParse(payload['clientModifiedAt'] as String? ?? '')
        : null;
    return server != null &&
        local != null &&
        server.microsecondsSinceEpoch >= local.microsecondsSinceEpoch;
  }

  /// Checks inside the transaction, so a run abandoned while waiting for the database (sign-out wipe) writes
  /// nothing.
  Future<void> _transaction(int generation, Future<void> Function() body) =>
      _db.transaction(() async {
        _ensureLive(generation);
        await body();
      });

  void _ensureLive(int generation) {
    if (generation != _generation) {
      throw const _Abandoned();
    }
  }

  void _clearBackoff() {
    _failures = 0;
    _retryAt = null;
  }

  Future<SyncCursorRow?> _storedCursor() => (_db.select(
    _db.syncCursors,
  )..where((c) => c.stream.equals(feedStream))).getSingleOrNull();

  Future<void> _saveCursor(int cursor, String? epoch) => _db
      .into(_db.syncCursors)
      .insertOnConflictUpdate(
        SyncCursorsCompanion.insert(
          stream: feedStream,
          cursor: cursor,
          epoch: Value(epoch),
        ),
      );

  /// No answer, or one that says nothing about this write (signed out, rate limited, gateway down): stop the
  /// run and retry later.
  static bool _isUnreachable(int status) =>
      status == 0 ||
      status == 401 ||
      status == 403 ||
      status == 408 ||
      status == 429 ||
      status == 502 ||
      status == 503 ||
      status == 504;

  /// The server looked at the write and said no; sending it again would not help.
  static bool _isRefusal(int status) =>
      status == 400 || status == 404 || status == 422;

  static String _message(ApiProblem problem) {
    final fields = problem.fieldErrors.values.expand((m) => m);
    if (fields.isNotEmpty) {
      return fields.join('\n');
    }
    if (_isRefusal(problem.status)) {
      return problem.title ?? 'Sunucu kaydı kabul etmedi.';
    }
    return 'Sunucu bu kaydı işleyemedi (HTTP ${problem.status}). Düzeltip kaydet ya da sil.';
  }
}

class _Transient implements Exception {
  const _Transient();
}

/// The run was abandoned by [SyncService.reset].
class _Abandoned implements Exception {
  const _Abandoned();
}
