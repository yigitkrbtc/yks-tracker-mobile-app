import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/database/app_database.dart';
import 'package:nirengi/core/network/api_problem.dart';
import 'package:nirengi/core/sync/outbox_store.dart';
import 'package:nirengi/core/sync/sync_handler.dart';
import 'package:nirengi/core/sync/sync_service.dart';

import '../support/sync_fakes.dart';

class RecordingHandler implements SyncHandler {
  final pushed = <String>[];
  final rejected = <String>[];
  final applied = <List<Map<String, dynamic>>>[];
  final queuedSeen = <Set<String>>[];
  Future<void> Function(OutboxRow item)? duringPush;

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
    applied.add(records);
    queuedSeen.add(queued);
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
    handler = RecordingHandler();
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
}
