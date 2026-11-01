import 'dart:io';

import 'package:drift/drift.dart' show Value;
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/database/app_database.dart';
import 'package:nirengi/core/sync/outbox_store.dart';
import 'package:nirengi/core/sync/sync_service.dart';
import 'package:nirengi/features/goals/data/drift_activity_repository.dart';
import 'package:nirengi/features/goals/domain/study_day.dart';
import 'package:nirengi/features/study_progress/data/drift_topic_progress_repository.dart';
import 'package:nirengi/features/study_progress/data/topic_progress_records.dart';
import 'package:nirengi/features/study_progress/data/topic_progress_sync_handler.dart';
import 'package:nirengi/features/study_progress/domain/topic_progress.dart';

import '../../support/sync_fakes.dart';

void main() {
  late AppDatabase db;
  late OutboxStore outbox;
  late FakeApi api;
  late DriftTopicProgressRepository repository;
  late SyncService sync;
  var now = DateTime.utc(2026, 11, 1, 9);

  /// Server side of PUT /v1/topic-progress: newest clientModifiedAt wins, answers the stored mark.
  final stored = <String, Map<String, dynamic>>{};
  var seq = 0;
  var feed = <Map<String, dynamic>>[];

  setUp(() {
    db = memoryDatabase();
    outbox = OutboxStore(db);
    api = FakeApi();
    stored.clear();
    seq = 0;
    feed = [];
    api.route = (r) {
      if (r.method == 'GET' && r.uri.path == '/v1/sync') {
        final body = {'topicProgress': feed, 'cursor': seq, 'hasMore': false};
        feed = [];
        return FakeApi.json(200, body);
      }
      final topicId = r.uri.pathSegments.last;
      if (topicId == 'retired') {
        return FakeApi.json(400, {
          'type': 'tracking.unknown_topic',
          'status': 400,
          'title': 'Konulardan biri müfredatta yok.',
        });
      }
      if (topicId == 'invalid') {
        return FakeApi.json(400, {
          'type': 'validation.failed',
          'status': 400,
          'title': 'Doğrulama hatası',
        });
      }
      final body = Map<String, dynamic>.from(r.data as Map);
      final current = stored[topicId];
      if (current == null ||
          DateTime.parse(body['clientModifiedAt'] as String)
              .isAfter(DateTime.parse(current['clientModifiedAt'] as String))) {
        stored[topicId] = {...body, 'topicId': topicId, 'changeSeq': ++seq};
      }
      return FakeApi.json(200, stored[topicId]);
    };
    repository = DriftTopicProgressRepository(db, outbox, now: () => now);
    sync = SyncService(db, outbox, api.dio, [
      TopicProgressSyncHandler(db, outbox),
    ]);
  });

  tearDown(() async {
    await sync.dispose();
    outbox.dispose();
    await db.close();
  });

  test(
    'a mark is shown at once, queued, then confirmed by the server',
    () async {
      await repository.mark('t1', ProgressStatus.inProgress);

      expect(await repository.watchAll().first, {
        't1': ProgressStatus.inProgress,
      });
      expect(await outbox.queuedCount(), 1);

      expect(await sync.sync(), SyncOutcome.done);
      expect(api.calls, contains('PUT /v1/topic-progress/t1'));
      expect(await outbox.queuedCount(), 0);
      final row = await db.select(db.topicProgressMarks).getSingle();
      expect(row.changeSeq, 1);
      expect(row.status, 'InProgress');
    },
  );

  test(
    'a newer mark replaces the queued one; an older server answer wins back',
    () async {
      await repository.mark('t1', ProgressStatus.inProgress);
      now = now.add(const Duration(minutes: 1));
      await repository.mark('t1', ProgressStatus.done);
      expect(await outbox.queuedCount(), 1);

      // Another device marked later still.
      stored['t1'] = {
        'topicId': 't1',
        'status': 'NotStarted',
        'clientModifiedAt': '2026-11-01T10:00:00.000Z',
        'changeSeq': ++seq,
      };
      await sync.sync();

      expect(await repository.watchAll().first, {
        't1': ProgressStatus.notStarted,
      });
    },
  );

  test('the change feed applies marks from other devices but not over a queued one', () async {
    await repository.mark('t1', ProgressStatus.done);
    api.route = (r) => r.method == 'GET'
        ? FakeApi.json(200, {
            'topicProgress': [
              {
                'topicId': 't1',
                'status': 'NotStarted',
                'clientModifiedAt': '2026-11-01T08:00:00Z',
                'changeSeq': 4,
              },
              {
                'topicId': 't2',
                'status': 'Done',
                'clientModifiedAt': '2026-11-01T08:00:00Z',
                'changeSeq': 5,
              },
            ],
            'cursor': 5,
            'hasMore': false,
          })
        // The push fails on the server, so the mark stays queued while the pull still runs.
        : FakeApi.json(500, {'status': 500});

    await sync.sync();

    expect(await repository.watchAll().first, {
      't1': ProgressStatus.done,
      't2': ProgressStatus.done,
    });
  });

  test('a mark on a topic the server refuses is dropped', () async {
    await repository.mark('retired', ProgressStatus.done);
    await sync.sync();
    expect(await repository.watchAll().first, isEmpty);
    expect(await outbox.queuedCount(), 0);
  });

  test('any other refusal keeps the mark and parks it in the outbox', () async {
    await repository.mark('invalid', ProgressStatus.done);
    await sync.sync();
    expect(await repository.watchAll().first, {'invalid': ProgressStatus.done});
    expect(await outbox.find(topicProgressEntity, 'invalid'), isNotNull);
  });

  test('payload carries the API status code and UTC edit time', () {
    expect(
      TopicProgressRecords.putPayload(
        'Done',
        DateTime.utc(2026, 11, 1, 9).microsecondsSinceEpoch,
      ),
      {'status': 'Done', 'clientModifiedAt': '2026-11-01T09:00:00.000Z'},
    );
  });

  test('daily activity sums topic test rows and solved mock sections, without deleted records', () async {
    await db
        .into(db.topicTests)
        .insert(
          TopicTestsCompanion.insert(
            id: 'a',
            studiedOn: '2026-11-02',
            isRepeat: false,
            clientModifiedAt: 1,
          ),
        );
    await db
        .into(db.topicTestResults)
        .insert(
          TopicTestResultsCompanion.insert(
            testId: 'a',
            topicId: 'x',
            total: 20,
            correct: 10,
            wrong: 5,
            blank: 5,
          ),
        );
    await db
        .into(db.topicTestResults)
        .insert(
          TopicTestResultsCompanion.insert(
            testId: 'a',
            topicId: 'y',
            total: 10,
            correct: 10,
            wrong: 0,
            blank: 0,
          ),
        );
    await db
        .into(db.topicTests)
        .insert(
          TopicTestsCompanion.insert(
            id: 'gone',
            studiedOn: '2026-11-02',
            isRepeat: false,
            clientModifiedAt: 1,
            deleted: const Value(true),
          ),
        );
    await db
        .into(db.topicTestResults)
        .insert(
          TopicTestResultsCompanion.insert(
            testId: 'gone',
            topicId: 'x',
            total: 99,
            correct: 0,
            wrong: 0,
            blank: 99,
          ),
        );
    await db
        .into(db.mockExams)
        .insert(
          MockExamsCompanion.insert(
            id: 'm',
            kind: 'Tyt',
            takenOn: '2026-11-03',
            clientModifiedAt: 1,
          ),
        );
    await db
        .into(db.mockExamSections)
        .insert(
          MockExamSectionsCompanion.insert(
            examId: 'm',
            sectionCode: 'TYT-TUR',
            questionCount: 40,
            correct: 30,
            wrong: 5,
          ),
        );
    await db
        .into(db.topicTests)
        .insert(
          TopicTestsCompanion.insert(
            id: 'old',
            studiedOn: '2026-10-01',
            isRepeat: false,
            clientModifiedAt: 1,
          ),
        );

    final activity = await DriftActivityRepository(db)
        .watchSince(DateTime.utc(2026, 10, 26))
        .first;

    expect(activity, [
      DayActivity(DateTime.utc(2026, 11, 2), 30),
      DayActivity(DateTime.utc(2026, 11, 3), 40),
    ]);
  });

  test(
    'a version 2 database gains the progress table and keeps its rows',
    () async {
      final dir = await Directory.systemTemp.createTemp('nirengi-v3');
      addTearDown(() => dir.delete(recursive: true));
      final file = File('${dir.path}/app.sqlite');

      final v2 = AppDatabase(NativeDatabase(file));
      await v2
          .into(v2.topicTests)
          .insert(
            TopicTestsCompanion.insert(
              id: 't1',
              studiedOn: '2026-10-30',
              isRepeat: false,
              clientModifiedAt: 1,
            ),
          );
      await v2.customStatement('DROP TABLE topic_progress_marks');
      await v2.customStatement('PRAGMA user_version = 2');
      await v2.close();

      final v3 = AppDatabase(NativeDatabase(file));
      addTearDown(v3.close);
      expect(await v3.select(v3.topicTests).get(), hasLength(1));
      await v3
          .into(v3.topicProgressMarks)
          .insert(
            TopicProgressMarksCompanion.insert(
              topicId: 'x',
              status: 'Done',
              clientModifiedAt: 1,
            ),
          );
      expect(await v3.select(v3.topicProgressMarks).get(), hasLength(1));
    },
  );
}
