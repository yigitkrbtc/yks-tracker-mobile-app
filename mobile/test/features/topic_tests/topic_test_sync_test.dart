import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/database/app_database.dart';
import 'package:nirengi/core/sync/outbox_store.dart';
import 'package:nirengi/core/sync/sync_service.dart';
import 'package:nirengi/features/topic_tests/data/drift_topic_test_repository.dart';
import 'package:nirengi/features/topic_tests/data/topic_test_sync_handler.dart';
import 'package:nirengi/features/topic_tests/domain/topic_test.dart';

import '../../support/sync_fakes.dart';

void main() {
  late AppDatabase db;
  late OutboxStore outbox;
  late FakeApi api;
  late FakeTrackingServer server;
  late DriftTopicTestRepository repository;
  late SyncService sync;
  var now = DateTime.utc(2026, 10, 31, 9);

  setUp(() {
    db = memoryDatabase();
    outbox = OutboxStore(db);
    api = FakeApi();
    server = FakeTrackingServer();
    api.route = server.handle;
    now = DateTime.utc(2026, 10, 31, 9);
    repository = DriftTopicTestRepository(db, outbox, now: () => now);
    sync = SyncService(db, outbox, api.dio, [
      TopicTestSyncHandler(db, outbox),
    ], now: () => now);
  });

  tearDown(() async {
    outbox.dispose();
    await db.close();
  });

  TopicTest sample({String id = 't1', int correct = 15, String? note}) =>
      TopicTest(
        id: id,
        studiedOn: DateTime.utc(2026, 10, 30),
        sourceId: 'src-1',
        difficulty: Difficulty.medium,
        durationMinutes: 40,
        note: note,
        results: [
          TopicResult(
            topicId: 'topic-a',
            total: 20,
            correct: correct,
            wrong: 4,
            mistakeKnowledge: 3,
          ),
        ],
        modifiedAt: now,
      );

  test('a saved test is listed at once and queued as the API body', () async {
    await repository.save(sample());

    final listed = await repository.watchAll().first;
    expect(listed.single.syncStatus, SyncStatus.pending);
    expect(listed.single.results.single.blank, 1);

    final queued = (await outbox.find('topic_test', 't1'))!;
    expect(queued.operation, 'put');
    expect(jsonDecode(queued.payload!), {
      'studiedOn': '2026-10-30',
      'sourceId': 'src-1',
      'sourceName': null,
      'isRepeat': false,
      'difficulty': 'Medium',
      'durationMinutes': 40,
      'note': null,
      'results': [
        {
          'topicId': 'topic-a',
          'total': 20,
          'correct': 15,
          'wrong': 4,
          'blank': 1,
          'mistakeKnowledge': 3,
          'mistakeAttention': null,
          'mistakeTime': null,
        },
      ],
      'clientModifiedAt': '2026-10-31T09:00:00.000Z',
    });
  });

  test('sync pushes, stores the server state and marks it synced', () async {
    await repository.save(sample());

    expect(await sync.sync(), SyncOutcome.done);

    expect(api.calls, [
      'PUT /v1/topic-tests/t1',
      'GET /v1/sync?since=0&limit=500',
    ]);
    final stored = (await repository.get('t1'))!;
    expect(stored.syncStatus, SyncStatus.synced);
    expect(stored.difficulty, Difficulty.medium);
    expect(await outbox.queuedCount(), 0);
    final row = await db.select(db.topicTests).getSingle();
    expect(row.changeSeq, 1);
  });

  test('offline keeps the test pending on the device', () async {
    api.route = FakeApi.offline;
    await repository.save(sample());

    expect(await sync.sync(), SyncOutcome.offline);
    expect((await repository.get('t1'))!.syncStatus, SyncStatus.pending);
  });

  test('a refused test shows the server message until it is edited', () async {
    server.reject = true;
    await repository.save(sample());
    await sync.sync();

    final refused = (await repository.get('t1'))!;
    expect(refused.syncStatus, SyncStatus.rejected);
    expect(refused.syncError, 'Konu bulunamadı.');

    server.reject = false;
    now = now.add(const Duration(minutes: 1));
    await repository.save(sample(correct: 14));
    expect((await repository.get('t1'))!.syncStatus, SyncStatus.pending);
    await sync.sync();
    expect((await repository.get('t1'))!.syncStatus, SyncStatus.synced);
    expect(firstResult(server.records['t1']!)['correct'], 14);
  });

  test('delete hides the test, sends modifiedAt, then drops the row', () async {
    await repository.save(sample());
    await sync.sync();
    now = now.add(const Duration(minutes: 5));

    await repository.delete('t1');
    expect(await repository.watchAll().first, isEmpty);
    expect(await repository.get('t1'), isNull);

    await sync.sync();
    expect(
      api.calls,
      contains(
        'DELETE /v1/topic-tests/t1?modifiedAt=2026-10-31T09%3A05%3A00.000Z',
      ),
    );
    expect(await db.select(db.topicTests).get(), isEmpty);
    expect(await db.select(db.topicTestResults).get(), isEmpty);
    expect(await outbox.queuedCount(), 0);
  });

  test(
    'the change feed adds other devices\' tests and applies deletes',
    () async {
      server.records['remote'] = {
        'id': 'remote',
        'studiedOn': '2026-10-29',
        'sourceId': null,
        'sourceName': 'Ders notu',
        'isRepeat': true,
        'difficulty': null,
        'durationMinutes': null,
        'note': 'telefon',
        'results': [
          {
            'topicId': 'topic-b',
            'total': 10,
            'correct': 7,
            'wrong': 2,
            'blank': 1,
            'net': 6.5,
          },
        ],
        'clientModifiedAt': '2026-10-29T18:30:00.123456+00:00',
        'updatedAt': '2026-10-29T18:30:01+00:00',
        'deleted': false,
        'changeSeq': ++server.seq,
      };

      await sync.sync();
      final remote = (await repository.get('remote'))!;
      expect(remote.sourceName, 'Ders notu');
      expect(remote.isRepeat, isTrue);
      expect(remote.results.single.net, 6.5);
      expect(remote.syncStatus, SyncStatus.synced);
      expect(
        remote.modifiedAt,
        DateTime.utc(2026, 10, 29, 18, 30, 0, 123, 456),
      );

      server.feed = [
        {
          'id': 'remote',
          'deleted': true,
          'changeSeq': ++server.seq,
          'results': <Object>[],
        },
      ];
      await sync.sync();
      expect(await repository.get('remote'), isNull);
    },
  );

  test('a queued local edit is not overwritten by the change feed', () async {
    await repository.save(sample());
    await sync.sync();
    now = now.add(const Duration(minutes: 1));
    server.reject = true; // keeps the edit queued while the feed is pulled
    await repository.save(sample(note: 'yerel'));
    server.feed = [
      {...server.records['t1']!, 'note': 'sunucu', 'changeSeq': ++server.seq},
    ];

    await sync.sync();

    final local = (await repository.get('t1'))!;
    expect(local.note, 'yerel');
    expect(local.syncStatus, SyncStatus.rejected);
  });

  test('last source preselects the next test', () async {
    expect(await repository.lastSource(), isNull);
    await repository.save(sample());
    now = now.add(const Duration(minutes: 1));
    await repository.save(
      TopicTest(
        id: 't2',
        studiedOn: DateTime.utc(2026, 10, 31),
        sourceName: 'Ders notu',
        results: const [
          TopicResult(topicId: 'topic-a', total: 5, correct: 5, wrong: 0),
        ],
        modifiedAt: now,
      ),
    );
    expect(await repository.lastSource(), (id: null, name: 'Ders notu'));
  });

  test(
    'a delete older than another device\'s edit brings the test back',
    () async {
      await repository.save(sample());
      await sync.sync();
      await repository.delete('t1');
      final stored = server.records['t1']!;
      api.route = (r) => r.method == 'DELETE'
          ? FakeApi.json(200, {...stored, 'note': 'tablet'})
          : server.handle(r);

      expect(await sync.sync(), SyncOutcome.done);

      final restored = (await repository.get('t1'))!;
      expect(restored.note, 'tablet');
      expect(restored.syncStatus, SyncStatus.synced);
      expect(await outbox.queuedCount(), 0);
    },
  );
}
