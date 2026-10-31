import 'package:drift/drift.dart';

import '../../../core/database/app_database.dart';
import '../domain/mock_exam.dart';

/// Outbox entity type of mock exams.
const mockExamEntity = 'mock_exam';

/// Row <-> API conversions shared by the repository and the sync handler.
abstract final class MockExamRecords {
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

  /// Body of PUT /v1/mock-exams/{id}. Question counts go along so a branch exam keeps its own.
  static Map<String, Object?> putPayload(MockExam e, int modifiedMicros) => {
    'kind': e.kind.code,
    'takenOn': dateKey(e.takenOn),
    'sourceId': e.sourceId,
    'sourceName': e.sourceName,
    'title': e.title,
    'durationMinutes': e.durationMinutes,
    'note': e.note,
    'sections': [
      for (final s in e.sections)
        {
          'sectionCode': s.sectionCode,
          'questionCount': s.questionCount,
          'correct': s.correct,
          'wrong': s.wrong,
        },
    ],
    'marks': [
      for (final m in e.marks)
        {
          'sectionCode': m.sectionCode,
          'topicId': m.topicId,
          'wrong': m.wrong,
          'blank': m.blank,
          'mistakeKnowledge': m.mistakeKnowledge,
          'mistakeAttention': m.mistakeAttention,
          'mistakeTime': m.mistakeTime,
        },
    ],
    'clientModifiedAt': isoMicros(modifiedMicros),
  };

  /// Writes [e] as the device's version: the row, then its sections and marks replaced.
  static Future<void> writeLocal(
    AppDatabase db,
    MockExam e, {
    required int modifiedMicros,
    int? changeSeq,
  }) async {
    await db
        .into(db.mockExams)
        .insertOnConflictUpdate(
          MockExamsCompanion(
            id: Value(e.id),
            kind: Value(e.kind.code),
            takenOn: Value(dateKey(e.takenOn)),
            sourceId: Value(e.sourceId),
            sourceName: Value(e.sourceName),
            title: Value(e.title),
            durationMinutes: Value(e.durationMinutes),
            note: Value(e.note),
            clientModifiedAt: Value(modifiedMicros),
            deleted: const Value(false),
            changeSeq: changeSeq == null
                ? const Value.absent()
                : Value(changeSeq),
          ),
        );
    await (db.delete(
      db.mockExamSections,
    )..where((s) => s.examId.equals(e.id))).go();
    await (db.delete(
      db.mockExamTopicMarks,
    )..where((m) => m.examId.equals(e.id))).go();
    await db.batch((b) {
      b.insertAll(db.mockExamSections, [
        for (final s in e.sections)
          MockExamSectionsCompanion.insert(
            examId: e.id,
            sectionCode: s.sectionCode,
            questionCount: s.questionCount,
            correct: s.correct,
            wrong: s.wrong,
          ),
      ]);
      b.insertAll(db.mockExamTopicMarks, [
        for (final m in e.marks)
          MockExamTopicMarksCompanion.insert(
            examId: e.id,
            sectionCode: m.sectionCode,
            topicId: m.topicId,
            wrong: m.wrong,
            blank: m.blank,
            mistakeKnowledge: Value(m.mistakeKnowledge),
            mistakeAttention: Value(m.mistakeAttention),
            mistakeTime: Value(m.mistakeTime),
          ),
      ]);
    });
  }

  /// Stores a record the server sent (push answer or change feed); a tombstone removes the local copy.
  static Future<void> storeServer(
    AppDatabase db,
    Map<String, dynamic> json,
  ) async {
    final id = json['id'] as String;
    if (json['deleted'] == true) {
      await (db.delete(db.mockExams)..where((e) => e.id.equals(id))).go();
      return;
    }
    final exam = MockExam(
      id: id,
      kind: MockExamKind.fromCode(json['kind'] as String),
      takenOn: parseDate(json['takenOn'] as String),
      sourceId: json['sourceId'] as String?,
      sourceName: json['sourceName'] as String?,
      title: json['title'] as String?,
      durationMinutes: json['durationMinutes'] as int?,
      note: json['note'] as String?,
      sections: [
        for (final s in json['sections'] as List<dynamic>)
          MockSectionResult(
            sectionCode: (s as Map<String, dynamic>)['sectionCode'] as String,
            questionCount: s['questionCount'] as int,
            correct: s['correct'] as int,
            wrong: s['wrong'] as int,
          ),
      ],
      marks: [
        for (final m in json['marks'] as List<dynamic>? ?? const [])
          TopicMark(
            sectionCode: (m as Map<String, dynamic>)['sectionCode'] as String,
            topicId: m['topicId'] as String,
            wrong: m['wrong'] as int,
            blank: m['blank'] as int,
            mistakeKnowledge: m['mistakeKnowledge'] as int?,
            mistakeAttention: m['mistakeAttention'] as int?,
            mistakeTime: m['mistakeTime'] as int?,
          ),
      ],
      modifiedAt: DateTime.parse(json['clientModifiedAt'] as String).toUtc(),
      syncStatus: SyncStatus.synced,
    );
    await writeLocal(
      db,
      exam,
      modifiedMicros: exam.modifiedAt.microsecondsSinceEpoch,
      changeSeq: json['changeSeq'] as int,
    );
  }

  static MockExam fromRows(
    MockExamRow row,
    List<MockExamSectionRow> sections,
    List<MockExamTopicMarkRow> marks,
    OutboxRow? queued,
  ) => MockExam(
    id: row.id,
    kind: MockExamKind.fromCode(row.kind),
    takenOn: parseDate(row.takenOn),
    sourceId: row.sourceId,
    sourceName: row.sourceName,
    title: row.title,
    durationMinutes: row.durationMinutes,
    note: row.note,
    sections: [
      for (final s in sections)
        MockSectionResult(
          sectionCode: s.sectionCode,
          questionCount: s.questionCount,
          correct: s.correct,
          wrong: s.wrong,
        ),
    ],
    marks: [
      for (final m in marks)
        TopicMark(
          sectionCode: m.sectionCode,
          topicId: m.topicId,
          wrong: m.wrong,
          blank: m.blank,
          mistakeKnowledge: m.mistakeKnowledge,
          mistakeAttention: m.mistakeAttention,
          mistakeTime: m.mistakeTime,
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
