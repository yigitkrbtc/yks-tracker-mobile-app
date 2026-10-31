import 'dart:convert';

import 'package:drift/drift.dart' show Variable;
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/database/app_database.dart';
import 'package:nirengi/core/sync/outbox_store.dart';
import 'package:nirengi/core/sync/sync_service.dart';
import 'package:nirengi/features/mock_exams/data/drift_mock_exam_repository.dart';
import 'package:nirengi/features/mock_exams/data/mock_exam_sync_handler.dart';
import 'package:nirengi/features/mock_exams/domain/mock_exam.dart';
import 'package:nirengi/features/topic_tests/data/topic_test_sync_handler.dart';

import '../../support/sync_fakes.dart';

void main() {
  late AppDatabase db;
  late OutboxStore outbox;
  late FakeApi api;
  late FakeTrackingServer server;
  late DriftMockExamRepository repository;
  late SyncService sync;
  var now = DateTime.utc(2026, 10, 31, 9);

  setUp(() {
    db = memoryDatabase();
    outbox = OutboxStore(db);
    api = FakeApi();
    server = FakeTrackingServer();
    api.route = server.handle;
    now = DateTime.utc(2026, 10, 31, 9);
    repository = DriftMockExamRepository(db, outbox, now: () => now);
    sync = SyncService(db, outbox, api.dio, [
      TopicTestSyncHandler(db, outbox),
      MockExamSyncHandler(db, outbox),
    ], now: () => now);
  });

  tearDown(() async {
    outbox.dispose();
    await db.close();
  });

  MockExam sample({List<TopicMark> marks = const []}) => MockExam(
    id: 'e1',
    kind: MockExamKind.tyt,
    takenOn: DateTime.utc(2026, 10, 30),
    sourceId: 'src-pub',
    title: 'Türkiye geneli 1',
    sections: const [
      MockSectionResult(
        sectionCode: 'TYT-TUR',
        questionCount: 40,
        correct: 30,
        wrong: 8,
      ),
      MockSectionResult(
        sectionCode: 'TYT-SOS.TAR',
        questionCount: 5,
        correct: 3,
        wrong: 1,
      ),
    ],
    marks: marks,
    modifiedAt: now,
  );

  test('a saved exam is listed at once and queued as the API body', () async {
    await repository.save(
      sample(
        marks: const [
          TopicMark(
            sectionCode: 'TYT-SOS.TAR',
            topicId: 'TYT.TAR-1',
            wrong: 1,
            blank: 1,
            mistakeAttention: 1,
          ),
        ],
      ),
    );

    final listed = (await repository.watchAll().first).single;
    expect(listed.syncStatus, SyncStatus.pending);
    expect(listed.net, 30.75);
    MockSectionResult section(String code) =>
        listed.sections.firstWhere((s) => s.sectionCode == code);
    expect(listed.taggingOf(section('TYT-SOS.TAR')), TaggingStatus.full);
    expect(listed.taggingOf(section('TYT-TUR')), TaggingStatus.none);
    expect(listed.needsTagging, isTrue);

    final queued = (await outbox.find('mock_exam', 'e1'))!;
    expect(jsonDecode(queued.payload!), {
      'kind': 'Tyt',
      'takenOn': '2026-10-30',
      'sourceId': 'src-pub',
      'sourceName': null,
      'title': 'Türkiye geneli 1',
      'durationMinutes': null,
      'note': null,
      'sections': [
        {
          'sectionCode': 'TYT-TUR',
          'questionCount': 40,
          'correct': 30,
          'wrong': 8,
        },
        {
          'sectionCode': 'TYT-SOS.TAR',
          'questionCount': 5,
          'correct': 3,
          'wrong': 1,
        },
      ],
      'marks': [
        {
          'sectionCode': 'TYT-SOS.TAR',
          'topicId': 'TYT.TAR-1',
          'wrong': 1,
          'blank': 1,
          'mistakeKnowledge': null,
          'mistakeAttention': 1,
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
      'PUT /v1/mock-exams/e1',
      'GET /v1/sync?since=0&limit=500',
    ]);
    final stored = (await repository.get('e1'))!;
    expect(stored.syncStatus, SyncStatus.synced);
    expect(stored.sections, hasLength(2));
    expect((await db.select(db.mockExams).getSingle()).changeSeq, 1);
  });

  test('tagging later is a new write of the whole exam', () async {
    await repository.save(sample());
    await sync.sync();
    now = now.add(const Duration(minutes: 3));

    final exam = (await repository.get('e1'))!;
    await repository.save(
      exam.copyWith(
        marks: const [
          TopicMark(
            sectionCode: 'TYT-TUR',
            topicId: 'TYT.TUR-1',
            wrong: 2,
            blank: 0,
          ),
        ],
      ),
    );
    await sync.sync();

    final marks = server.exams['e1']!['marks'] as List<dynamic>;
    expect((marks.single as Map<String, dynamic>)['wrong'], 2);
    expect((await repository.get('e1'))!.marks.single.topicId, 'TYT.TUR-1');
  });

  test('a refused exam shows the server message', () async {
    server.reject = true;
    await repository.save(sample());
    await sync.sync();

    final refused = (await repository.get('e1'))!;
    expect(refused.syncStatus, SyncStatus.rejected);
    expect(refused.syncError, isNotNull);
  });

  test('delete hides the exam and the tombstone removes the row', () async {
    await repository.save(sample());
    await sync.sync();
    now = now.add(const Duration(minutes: 5));

    await repository.delete('e1');
    expect(await repository.watchAll().first, isEmpty);
    final queued = (await outbox.find('mock_exam', 'e1'))!;
    expect(jsonDecode(queued.payload!), {
      'modifiedAt': '2026-10-31T09:05:00.000Z',
    });

    await sync.sync();
    expect(api.calls, contains(startsWith('DELETE /v1/mock-exams/e1')));
    expect(await db.select(db.mockExams).get(), isEmpty);
    expect(await db.select(db.mockExamSections).get(), isEmpty);
  });

  test('another device\'s exam arrives with the change feed', () async {
    server.exams['e9'] = {
      'id': 'e9',
      'kind': 'Branch',
      'takenOn': '2026-10-29',
      'sourceId': null,
      'sourceName': 'Kendi kitabım',
      'title': null,
      'durationMinutes': 40,
      'note': null,
      'sections': [
        {
          'sectionCode': 'AYT-FEN.FIZ',
          'questionCount': 20,
          'correct': 12,
          'wrong': 4,
        },
      ],
      'marks': <Object>[],
      'clientModifiedAt': '2026-10-29T10:00:00.000Z',
      'deleted': false,
      'changeSeq': ++server.seq,
    };

    await sync.sync();

    final exam = (await repository.get('e9'))!;
    expect(exam.kind, MockExamKind.branch);
    expect(exam.sections.single.blank, 4);
    expect(exam.sourceName, 'Kendi kitabım');
  });

  test('lastSource returns the most recently edited publisher', () async {
    await repository.save(sample());
    expect(await repository.lastSource(), (id: 'src-pub', name: null));
  });

  test(
    'a version 1 database gains the mock exam tables and keeps its rows',
    () async {
      final raw = NativeDatabase.memory(
        setup: (d) {
          d.execute(
            'CREATE TABLE topic_tests (id TEXT NOT NULL PRIMARY KEY, studied_on TEXT NOT NULL, '
            'source_id TEXT NULL, source_name TEXT NULL, is_repeat INTEGER NOT NULL, difficulty TEXT NULL, '
            'duration_minutes INTEGER NULL, note TEXT NULL, client_modified_at INTEGER NOT NULL, '
            'deleted INTEGER NOT NULL DEFAULT 0, change_seq INTEGER NOT NULL DEFAULT 0)',
          );
          d.execute(
            "INSERT INTO topic_tests (id, studied_on, is_repeat, client_modified_at) VALUES ('t1', '2026-10-30', 0, 1)",
          );
          d.execute('PRAGMA user_version = 1');
        },
      );
      final old = AppDatabase(raw);
      addTearDown(old.close);

      expect(await old.select(old.topicTests).get(), hasLength(1));
      expect(await old.select(old.mockExams).get(), isEmpty);
      final version = await old
          .customSelect('PRAGMA user_version', variables: <Variable>[])
          .getSingle();
      expect(version.data.values.single, 2);
    },
  );
}
