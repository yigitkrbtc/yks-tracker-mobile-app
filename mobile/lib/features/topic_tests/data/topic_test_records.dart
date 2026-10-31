import 'package:drift/drift.dart';

import '../../../core/database/app_database.dart';
import '../domain/topic_test.dart';

/// Outbox entity type of topic tests.
const topicTestEntity = 'topic_test';

/// Row <-> API conversions shared by the repository and the sync handler.
abstract final class TopicTestRecords {
  static String dateKey(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-'
      '${d.month.toString().padLeft(2, '0')}-'
      '${d.day.toString().padLeft(2, '0')}';

  static DateTime parseDate(String key) {
    final p = key.split('-').map(int.parse).toList();
    return DateTime.utc(p[0], p[1], p[2]);
  }

  static String isoMicros(int micros) => DateTime.fromMicrosecondsSinceEpoch(
    micros,
    isUtc: true,
  ).toIso8601String();

  /// Body of PUT /v1/topic-tests/{id}.
  static Map<String, Object?> putPayload(TopicTest t, int modifiedMicros) => {
    'studiedOn': dateKey(t.studiedOn),
    'sourceId': t.sourceId,
    'sourceName': t.sourceName,
    'isRepeat': t.isRepeat,
    'difficulty': t.difficulty?.code,
    'durationMinutes': t.durationMinutes,
    'note': t.note,
    'results': [
      for (final r in t.results)
        {
          'topicId': r.topicId,
          'total': r.total,
          'correct': r.correct,
          'wrong': r.wrong,
          'blank': r.blank,
          'mistakeKnowledge': r.mistakeKnowledge,
          'mistakeAttention': r.mistakeAttention,
          'mistakeTime': r.mistakeTime,
        },
    ],
    'clientModifiedAt': isoMicros(modifiedMicros),
  };

  /// Writes [t] as the device's version: the row, then its results replaced.
  static Future<void> writeLocal(
    AppDatabase db,
    TopicTest t, {
    required int modifiedMicros,
    int? changeSeq,
  }) async {
    await db
        .into(db.topicTests)
        .insertOnConflictUpdate(
          TopicTestsCompanion(
            id: Value(t.id),
            studiedOn: Value(dateKey(t.studiedOn)),
            sourceId: Value(t.sourceId),
            sourceName: Value(t.sourceName),
            isRepeat: Value(t.isRepeat),
            difficulty: Value(t.difficulty?.code),
            durationMinutes: Value(t.durationMinutes),
            note: Value(t.note),
            clientModifiedAt: Value(modifiedMicros),
            deleted: const Value(false),
            changeSeq: changeSeq == null
                ? const Value.absent()
                : Value(changeSeq),
          ),
        );
    await (db.delete(
      db.topicTestResults,
    )..where((r) => r.testId.equals(t.id))).go();
    await db.batch(
      (b) => b.insertAll(db.topicTestResults, [
        for (final r in t.results)
          TopicTestResultsCompanion.insert(
            testId: t.id,
            topicId: r.topicId,
            total: r.total,
            correct: r.correct,
            wrong: r.wrong,
            blank: r.blank,
            mistakeKnowledge: Value(r.mistakeKnowledge),
            mistakeAttention: Value(r.mistakeAttention),
            mistakeTime: Value(r.mistakeTime),
          ),
      ]),
    );
  }

  /// Stores a record the server sent (push answer or change feed); a tombstone removes the local copy.
  static Future<void> storeServer(
    AppDatabase db,
    Map<String, dynamic> json,
  ) async {
    final id = json['id'] as String;
    if (json['deleted'] == true) {
      await (db.delete(db.topicTests)..where((t) => t.id.equals(id))).go();
      return;
    }
    final test = TopicTest(
      id: id,
      studiedOn: parseDate(json['studiedOn'] as String),
      sourceId: json['sourceId'] as String?,
      sourceName: json['sourceName'] as String?,
      isRepeat: json['isRepeat'] as bool,
      difficulty: Difficulty.fromCode(json['difficulty'] as String?),
      durationMinutes: json['durationMinutes'] as int?,
      note: json['note'] as String?,
      results: [
        for (final r in json['results'] as List<dynamic>)
          TopicResult(
            topicId: (r as Map<String, dynamic>)['topicId'] as String,
            total: r['total'] as int,
            correct: r['correct'] as int,
            wrong: r['wrong'] as int,
            mistakeKnowledge: r['mistakeKnowledge'] as int?,
            mistakeAttention: r['mistakeAttention'] as int?,
            mistakeTime: r['mistakeTime'] as int?,
          ),
      ],
      modifiedAt: DateTime.parse(json['clientModifiedAt'] as String).toUtc(),
      syncStatus: SyncStatus.synced,
    );
    await writeLocal(
      db,
      test,
      modifiedMicros: test.modifiedAt.microsecondsSinceEpoch,
      changeSeq: json['changeSeq'] as int,
    );
  }

  static TopicTest fromRows(
    TopicTestRow row,
    List<TopicResultRow> results,
    OutboxRow? queued,
  ) => TopicTest(
    id: row.id,
    studiedOn: parseDate(row.studiedOn),
    sourceId: row.sourceId,
    sourceName: row.sourceName,
    isRepeat: row.isRepeat,
    difficulty: Difficulty.fromCode(row.difficulty),
    durationMinutes: row.durationMinutes,
    note: row.note,
    results: [
      for (final r in results)
        TopicResult(
          topicId: r.topicId,
          total: r.total,
          correct: r.correct,
          wrong: r.wrong,
          mistakeKnowledge: r.mistakeKnowledge,
          mistakeAttention: r.mistakeAttention,
          mistakeTime: r.mistakeTime,
        ),
    ],
    modifiedAt: DateTime.fromMicrosecondsSinceEpoch(
      row.clientModifiedAt,
      isUtc: true,
    ),
    syncStatus: queued == null
        ? SyncStatus.synced
        : queued.rejected
        ? SyncStatus.rejected
        : SyncStatus.pending,
    syncError: queued?.lastError,
  );
}
