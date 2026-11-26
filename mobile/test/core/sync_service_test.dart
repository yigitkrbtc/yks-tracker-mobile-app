import 'dart:io';

import 'package:dio/dio.dart';
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/database/app_database.dart';
import 'package:nirengi/core/network/api_problem.dart';
import 'package:nirengi/core/sync/outbox_store.dart';
import 'package:nirengi/core/sync/sync_handler.dart';
import 'package:nirengi/core/sync/sync_service.dart';

import '../support/sync_fakes.dart';

class RecordingHandler implements SyncHandler {
  RecordingHandler(this._outbox);

  final OutboxStore _outbox;

  /// Ids of the records this fake device holds.
  final local = <String>[];

  /// Edit time each local record carries in its put body.
  static const localModifiedAt = '2026-10-30T10:00:00.000Z';
  final requeuedWith = <Set<String>>[];
  final pushed = <String>[];
  final rejected = <String>[];
  final applied = <List<Map<String, dynamic>>>[];
  final queuedSeen = <Set<String>>[];
  Future<void> Function(OutboxRow item)? duringPush;
  Exception? applyError;
  Future<void> Function()? duringApply;
  Future<void> Function()? duringRequeue;

  @override
  String get entityType => 'thing';

  @override
  String get feedKey => 'things';

  @override
  Future<Object?> push(Dio api, OutboxRow item) async {
    await duringPush?.call(item);
    final response = await api.put<Object?>('/v1/things/${item.entityId}');
    return response.data;
  }

  @override
  Future<void> onPushed(OutboxRow item, Object? response) async =>
      pushed.add(item.entityId);

  @override
  Future<void> onRejected(OutboxRow item, ApiProblem problem) async =>
      rejected.add(item.entityId);

  @override
  Future<void> applyRemote(
    List<Map<String, dynamic>> records,
    Set<String> queued,
  ) async {
    if (applyError != null) {
      throw applyError!;
    }
    applied.add(records);
    queuedSeen.add(queued);
    await duringApply?.call();
  }

  @override
  String recordId(Map<String, dynamic> record) => record['id'] as String;

  @override
  Future<void> requeueAll(Set<String> queued) async {
    requeuedWith.add(queued);
    final during = duringRequeue;
    duringRequeue = null;
    for (final id in local) {
      if (!queued.contains(id)) {
        await _outbox.enqueue(
          entityType: entityType,
          entityId: id,
          operation: 'put',
          payload: {'id': id, 'clientModifiedAt': localModifiedAt},
        );
      }
    }
    await during?.call();
  }
}

void main() {
  late AppDatabase db;
  late OutboxStore outbox;
  late FakeApi api;
  late RecordingHandler handler;
  late DateTime now;
  late SyncService service;

  setUp(() {
    db = memoryDatabase();
    outbox = OutboxStore(db);
    api = FakeApi();
    handler = RecordingHandler(outbox);
    now = DateTime.utc(2026, 10, 31, 12);
    service = SyncService(db, outbox, api.dio, [handler], now: () => now);
  });

  tearDown(() async {
    outbox.dispose();
    await db.close();
  });

  Future<void> queue(String id, [String op = 'put']) => outbox.enqueue(
    entityType: 'thing',
    entityId: id,
    operation: op,
    payload: {'id': id},
  );

  ResponseBody feed(
    RequestOptions r, {
    List<Map<String, Object?>> things = const [],
    int cursor = 0,
    bool hasMore = false,
  }) => FakeApi.json(200, {
    'things': things,
    'cursor': cursor,
    'hasMore': hasMore,
  });

  test('announces only runs that pushed everything and caught up', () async {
    final completed = <void>[];
    final sub = service.completed.listen(completed.add);
    addTearDown(sub.cancel);
    await queue('a');
    api.route = (r) => FakeApi.json(503, null);

    expect(await service.sync(), SyncOutcome.offline);
    await Future<void>.delayed(Duration.zero);
    expect(completed, isEmpty);

    api.route = (r) => r.method == 'PUT' ? FakeApi.json(200, {}) : feed(r);
    expect(await service.sync(force: true), SyncOutcome.done);
    await Future<void>.delayed(Duration.zero);
    expect(completed, hasLength(1));
  });

  test('a newer write of the same record replaces the queued one', () async {
    await queue('a');
    await queue('a', 'delete');

    final rows = await outbox.pushable();
    expect(rows, hasLength(1));
    expect(rows.single.operation, 'delete');
    expect(rows.single.revision, 1);
    expect(await outbox.queuedCount(), 1);
  });

  test(
    'pushes queued writes oldest first, then pulls and keeps the cursor',
    () async {
      await queue('a');
      await queue('b');
      api.route = (r) => r.method == 'PUT'
          ? FakeApi.json(200, {})
          : feed(
              r,
              things: [
                {'id': 'x'},
              ],
              cursor: 7,
            );

      expect(await service.sync(), SyncOutcome.done);

      expect(handler.pushed, ['a', 'b']);
      expect(await outbox.queuedCount(), 0);
      expect(handler.applied.single.single['id'], 'x');
      api.requests.clear();
      await service.sync();
      expect(api.calls.last, 'GET /v1/sync?since=7&limit=500');
    },
  );

  test(
    'offline keeps the queue and backs off until the retry is due',
    () async {
      await queue('a');
      api.route = FakeApi.offline;

      expect(await service.sync(), SyncOutcome.offline);
      expect(await outbox.queuedCount(), 1);
      expect(service.retryAt, now.add(const Duration(seconds: 2)));

      expect(await service.sync(), SyncOutcome.deferred);
      now = now.add(const Duration(seconds: 3));
      expect(await service.sync(), SyncOutcome.offline);
      expect(service.retryAt, now.add(const Duration(seconds: 4)));

      api.route = (r) => r.method == 'PUT' ? FakeApi.json(200, {}) : feed(r);
      expect(await service.sync(force: true), SyncOutcome.done);
      expect(service.retryAt, isNull);
      expect(handler.pushed, ['a']);
    },
  );

  test('a refused write is parked and the rest still go', () async {
    await queue('bad');
    await queue('good');
    api.route = (r) => switch (r.uri.path) {
      '/v1/things/bad' => FakeApi.json(400, {
        'type': 'validation.failed',
        'title': 'x',
        'errors': {
          'Results': ['Hatalı'],
        },
      }),
      '/v1/things/good' => FakeApi.json(200, {}),
      _ => feed(r),
    };

    expect(await service.sync(), SyncOutcome.done);

    expect(handler.rejected, ['bad']);
    expect(handler.pushed, ['good']);
    expect(await outbox.pushable(), isEmpty);
    final parked = await outbox.find('thing', 'bad');
    expect(parked!.rejected, isTrue);
    expect(parked.lastError, 'Hatalı');

    await queue('bad'); // the student fixed it
    expect((await outbox.pushable()).single.entityId, 'bad');
  });

  test(
    'a write made while its older version is in flight stays queued',
    () async {
      await queue('a');
      handler.duringPush = (item) async {
        if (item.revision == 0) {
          await queue('a');
        }
      };
      api.route = (r) => r.method == 'PUT' ? FakeApi.json(200, {}) : feed(r);

      await service.sync();

      expect(
        handler.pushed,
        isEmpty,
        reason: 'the stale answer must not mark the newer write as synced',
      );
      final pending = (await outbox.pushable()).single;
      expect(pending.revision, 1);

      await service.sync();
      expect(handler.pushed, ['a']);
    },
  );

  test('pull pages through the feed and tells handlers which records have local writes', () async {
    await queue('mine');
    api.route = (r) {
      if (r.method == 'PUT') {
        return FakeApi.offline(r);
      }
      return FakeApi.json(200, {});
    };
    expect(await service.sync(), SyncOutcome.offline);

    var page = 0;
    api.route = (r) {
      if (r.method == 'PUT') {
        return FakeApi.json(400, {'type': 'validation.failed'});
      }
      page++;
      return page == 1
          ? feed(
              r,
              things: [
                {'id': '1'},
              ],
              cursor: 3,
              hasMore: true,
            )
          : feed(
              r,
              things: [
                {'id': '2'},
              ],
              cursor: 5,
            );
    };
    expect(await service.sync(force: true), SyncOutcome.done);

    expect(handler.applied.map((p) => p.single['id']), ['1', '2']);
    expect(handler.queuedSeen.first, {
      'mine',
    }, reason: 'a refused write still shields the local record');
  });

  test('a write the server keeps failing on is skipped, then parked', () async {
    await queue('broken');
    await queue('good');
    api.route = (r) => switch (r.uri.path) {
      '/v1/things/broken' => FakeApi.json(500, {'title': 'boom'}),
      '/v1/things/good' => FakeApi.json(200, {}),
      _ => feed(r, cursor: 4),
    };

    expect(await service.sync(), SyncOutcome.offline);
    expect(handler.pushed, ['good'], reason: 'later writes still go');
    expect(api.calls.last, 'GET /v1/sync?since=0&limit=500');
    expect(service.retryIn, const Duration(seconds: 2));

    for (var i = 1; i < SyncService.maxServerFailures; i++) {
      await service.sync(force: true);
    }
    final parked = await outbox.find('thing', 'broken');
    expect(parked!.rejected, isTrue);
    expect(parked.lastError, contains('HTTP 500'));
    expect(handler.rejected, ['broken']);
    expect(await service.sync(force: true), SyncOutcome.done);
  });

  test('401 stops the run without counting against the write', () async {
    await queue('a');
    api.route = (r) => FakeApi.json(401, {});

    expect(await service.sync(), SyncOutcome.offline);
    expect(api.calls, ['PUT /v1/things/a']);
    for (var i = 0; i < SyncService.maxServerFailures; i++) {
      await service.sync(force: true);
    }
    expect((await outbox.find('thing', 'a'))!.rejected, isFalse);
  });

  test(
    'a feed this version cannot read backs off instead of throwing',
    () async {
      api.route = (r) =>
          FakeApi.json(200, {'things': <Object>[], 'cursor': 'x'});

      expect(await service.sync(), SyncOutcome.failed);
      expect(service.retryAt, now.add(const Duration(seconds: 2)));
    },
  );

  test('an unreadable record reports failed, not offline', () async {
    final record = <String, Object?>{'id': 'x'};
    api.route = (r) => feed(r, things: [record], cursor: 3);
    handler.applyError = const FormatException('unknown record');

    expect(await service.sync(), SyncOutcome.failed);
    expect(service.retryAt, now.add(const Duration(seconds: 2)));
    expect(await db.select(db.syncCursors).get(), isEmpty);

    handler.applyError = null;
    expect(await service.sync(force: true), SyncOutcome.done);
    expect(service.retryAt, isNull);
    expect(handler.applied.single.single['id'], 'x');
  });

  test('a sync asked for during a run runs again after it', () async {
    var pulls = 0;
    api.route = (r) {
      if (r.method == 'GET') {
        pulls++;
      }
      return r.method == 'PUT' ? FakeApi.json(200, {}) : feed(r);
    };
    handler.duringPush = null;
    final first = service.sync();
    await queue('late');
    final joined = service.sync();

    expect(await first, SyncOutcome.done);
    expect(await joined, SyncOutcome.done);
    expect(handler.pushed, ['late']);
    expect(pulls, 2);
  });

  test('reset abandons a run: nothing more is written or sent', () async {
    await queue('a');
    late Future<SyncOutcome> run;
    api.route = (r) {
      if (r.method == 'PUT') {
        service.reset(); // sign-out while the push is in flight
        return FakeApi.json(200, {});
      }
      return feed(r, cursor: 9);
    };

    run = service.sync();
    expect(await run, SyncOutcome.deferred);
    expect(handler.pushed, isEmpty);
    expect(api.calls, ['PUT /v1/things/a'], reason: 'no pull after reset');
    expect(await db.select(db.syncCursors).get(), isEmpty);

    api.route = (r) => r.method == 'PUT' ? FakeApi.json(200, {}) : feed(r);
    expect(await service.sync(), SyncOutcome.done);
    expect(handler.pushed, ['a']);
  });

  group('sync epoch (ADR-0022 §4)', () {
    ResponseBody epochFeed(RequestOptions r, String? epoch, {int cursor = 0}) =>
        FakeApi.json(200, {
          'things': <Object>[],
          'cursor': cursor,
          'hasMore': false,
          'epoch': ?epoch,
        });

    Future<SyncCursorRow?> stored() => (db.select(
      db.syncCursors,
    )..where((c) => c.stream.equals(SyncService.feedStream))).getSingleOrNull();

    test(
      'the first epoch seen is stored and nothing is queued again',
      () async {
        handler.local.add('a');
        api.route = (r) => epochFeed(r, 'e1', cursor: 7);

        expect(await service.sync(), SyncOutcome.done);

        expect(handler.requeuedWith, isEmpty);
        expect(await outbox.queuedCount(), 0);
        final row = await stored();
        expect([row?.cursor, row?.epoch], [7, 'e1']);
      },
    );

    test('a new epoch resets the cursor and sends every local record again in the same run', () async {
      handler.local.addAll(['a', 'b']);
      api.route = (r) => epochFeed(r, 'e1', cursor: 7);
      await service.sync();

      await queue('b');
      api.requests.clear();
      var restoredPage = false;
      api.route = (r) {
        if (r.method == 'PUT') {
          // The server refuses b for good: it stays parked, not queued again.
          return r.uri.path.endsWith('/b')
              ? FakeApi.json(400, {'type': 'validation.failed'})
              : FakeApi.json(200, {});
        }
        restoredPage = true;
        return epochFeed(r, 'e2', cursor: 2);
      };

      expect(await service.sync(), SyncOutcome.done);

      expect(restoredPage, isTrue);
      expect(
        handler.requeuedWith,
        [
          {'b'},
        ],
        reason:
            'a record with a queued (here parked) write is not queued again',
      );
      expect(api.calls, [
        'PUT /v1/things/b',
        'GET /v1/sync?since=7&limit=500',
        'GET /v1/sync?since=0&limit=500',
        'PUT /v1/things/a',
        'GET /v1/sync?since=2&limit=500',
      ]);
      expect(handler.applied, isEmpty);
      expect(handler.rejected, ['b']);
      expect((await outbox.pushable()), isEmpty);
      final row = await stored();
      expect([row?.cursor, row?.epoch], [2, 'e2']);
    });

    test('the same epoch, or a feed without one, queues nothing', () async {
      handler.local.add('a');
      api.route = (r) => epochFeed(r, 'e1', cursor: 3);
      await service.sync();

      api.route = (r) => epochFeed(r, 'e1', cursor: 4);
      await service.sync();
      api.route = (r) => epochFeed(r, null, cursor: 5);
      await service.sync();

      expect(handler.requeuedWith, isEmpty);
      final row = await stored();
      expect([row?.cursor, row?.epoch], [5, 'e1']);
    });
  });

  group('sync epoch: sending only what the restored server lacks', () {
    ResponseBody page(
      String epoch, {
      int cursor = 0,
      bool hasMore = false,
      List<Map<String, Object?>> things = const [],
    }) => FakeApi.json(200, {
      'things': things,
      'cursor': cursor,
      'hasMore': hasMore,
      'epoch': epoch,
    });

    Future<void> firstEpoch(String epoch, {int cursor = 7}) async {
      api.route = (r) => page(epoch, cursor: cursor);
      await service.sync();
      api.requests.clear();
    }

    test('records the restored server already has (same or newer) are not sent again', () async {
      handler.local.addAll(['same', 'newer', 'older', 'missing']);
      await firstEpoch('e1');
      api.route = (r) {
        if (r.method == 'PUT') {
          return FakeApi.json(200, {});
        }
        return page(
          'e2',
          cursor: 4,
          things: [
            {'id': 'same', 'clientModifiedAt': '2026-10-30T10:00:00Z'},
            {'id': 'newer', 'clientModifiedAt': '2026-10-30T11:00:00Z'},
            {'id': 'older', 'clientModifiedAt': '2026-10-30T09:00:00Z'},
          ],
        );
      };

      expect(await service.sync(), SyncOutcome.done);

      expect(
        api.calls.where((c) => c.startsWith('PUT')),
        unorderedEquals(['PUT /v1/things/older', 'PUT /v1/things/missing']),
      );
      expect(handler.applied.first.map((r) => r['id']), [
        'same',
        'newer',
        'older',
      ]);
      expect(handler.queuedSeen.first, {
        'older',
        'missing',
      }, reason: 'pruned records take the server state');
      expect(await outbox.queuedCount(), 0);
    });

    test('a write made after the requeue is never pruned', () async {
      handler.local.add('a');
      await firstEpoch('e1');
      api.route = (r) {
        if (r.method == 'PUT') {
          return FakeApi.offline(r);
        }
        return page(
          'e2',
          cursor: 4,
          things: [
            {'id': 'a', 'clientModifiedAt': '2026-10-30T12:00:00Z'},
          ],
        );
      };
      // The student edits a while the restored feed is being read: the requeued write is replaced.
      handler.duringRequeue = () => queue('a');

      await service.sync();

      final item = await outbox.find('thing', 'a');
      expect(item, isNotNull);
      expect(item!.revision, 1);
    });

    test('an epoch change on a later page restarts the feed from 0', () async {
      handler.local.add('a');
      await firstEpoch('e1', cursor: 1);
      api.route = (r) {
        if (r.method == 'PUT') {
          return FakeApi.json(200, {});
        }
        final since = r.uri.queryParameters['since'];
        return since == '1'
            ? page('e1', cursor: 3, hasMore: true)
            : page('e2', cursor: 2);
      };

      expect(await service.sync(), SyncOutcome.done);

      expect(api.calls, [
        'GET /v1/sync?since=1&limit=500',
        'GET /v1/sync?since=3&limit=500',
        'GET /v1/sync?since=0&limit=500',
        'PUT /v1/things/a',
        'GET /v1/sync?since=2&limit=500',
      ]);
    });

    test(
      'a device that synced before epochs existed sends its records once',
      () async {
        handler.local.add('a');
        await db
            .into(db.syncCursors)
            .insert(
              SyncCursorsCompanion.insert(
                stream: SyncService.feedStream,
                cursor: 900,
              ),
            );
        api.route = (r) =>
            r.method == 'PUT' ? FakeApi.json(200, {}) : page('e1', cursor: 5);

        expect(await service.sync(), SyncOutcome.done);

        expect(api.calls, [
          'GET /v1/sync?since=900&limit=500',
          'GET /v1/sync?since=0&limit=500',
          'PUT /v1/things/a',
          'GET /v1/sync?since=5&limit=500',
        ]);
      },
    );

    test(
      'a write that failed earlier in the run is not retried in it',
      () async {
        await firstEpoch('e1');
        await queue('x');
        api.route = (r) =>
            r.method == 'PUT' ? FakeApi.json(500, null) : page('e2', cursor: 1);

        expect(await service.sync(), SyncOutcome.offline);

        expect(api.calls.where((c) => c == 'PUT /v1/things/x'), hasLength(1));
      },
    );
  });

  test('a version 3 database keeps its cursor and has no epoch yet', () async {
    final dir = await Directory.systemTemp.createTemp('nirengi-v4');
    addTearDown(() => dir.delete(recursive: true));
    final file = File('${dir.path}/app.sqlite');
    final v3 = AppDatabase(NativeDatabase(file));
    await v3
        .into(v3.syncCursors)
        .insert(SyncCursorsCompanion.insert(stream: 'main', cursor: 42));
    await v3.customStatement('ALTER TABLE sync_cursors DROP COLUMN epoch');
    await v3.customStatement('PRAGMA user_version = 3');
    await v3.close();

    final v4 = AppDatabase(NativeDatabase(file));
    addTearDown(v4.close);
    final row = await v4.select(v4.syncCursors).getSingle();
    expect([row.cursor, row.epoch], [42, null]);
  });
}
