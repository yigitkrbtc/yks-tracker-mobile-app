import 'package:drift/drift.dart';

import '../../../core/database/app_database.dart';
import '../../../core/sync/outbox_store.dart';
import '../domain/mock_exam.dart';
import '../domain/mock_exam_repository.dart';
import 'mock_exam_records.dart';

/// Mock exams in the device database. Each write and its queued sync request share one transaction (ADR-0014).
class DriftMockExamRepository implements MockExamRepository {
  DriftMockExamRepository(this._db, this._outbox, {DateTime Function()? now})
    : _now = now ?? DateTime.now;

  final AppDatabase _db;
  final OutboxStore _outbox;
  final DateTime Function() _now;

  @override
  Stream<List<MockExam>> watchAll() {
    final e = _db.mockExams;
    final o = _db.outbox;
    final query =
        _db.select(e).join([
            leftOuterJoin(
              o,
              o.entityId.equalsExp(e.id) & o.entityType.equals(mockExamEntity),
            ),
          ])
          ..where(e.deleted.equals(false))
          ..orderBy([
            OrderingTerm.desc(e.takenOn),
            OrderingTerm.desc(e.clientModifiedAt),
          ]);
    // Sections and marks change only together with their exam row, so watching the join is enough.
    return query.watch().asyncMap((rows) async {
      final ids = [for (final r in rows) r.readTable(e).id];
      final sections = await (_db.select(
        _db.mockExamSections,
      )..where((s) => s.examId.isIn(ids))).get();
      final marks = await (_db.select(
        _db.mockExamTopicMarks,
      )..where((m) => m.examId.isIn(ids))).get();
      return [
        for (final r in rows)
          MockExamRecords.fromRows(
            r.readTable(e),
            [
              for (final s in sections)
                if (s.examId == r.readTable(e).id) s,
            ],
            [
              for (final m in marks)
                if (m.examId == r.readTable(e).id) m,
            ],
            r.readTableOrNull(o),
          ),
      ];
    });
  }

  @override
  Future<MockExam?> get(String id) async {
    final row =
        await (_db.select(_db.mockExams)
              ..where((e) => e.id.equals(id) & e.deleted.equals(false)))
            .getSingleOrNull();
    if (row == null) {
      return null;
    }
    final sections = await (_db.select(
      _db.mockExamSections,
    )..where((s) => s.examId.equals(id))).get();
    final marks = await (_db.select(
      _db.mockExamTopicMarks,
    )..where((m) => m.examId.equals(id))).get();
    return MockExamRecords.fromRows(
      row,
      sections,
      marks,
      await _outbox.find(mockExamEntity, id),
    );
  }

  @override
  Future<void> save(MockExam exam) => _db.transaction(() async {
    final micros = _nowMicros();
    await MockExamRecords.writeLocal(_db, exam, modifiedMicros: micros);
    await _outbox.enqueue(
      entityType: mockExamEntity,
      entityId: exam.id,
      operation: 'put',
      payload: MockExamRecords.putPayload(exam, micros),
    );
  });

  /// Hides the exam at once; the row goes when the server confirms. An exam the server never saw still sends
  /// the delete (answered 204), which keeps a single queued write per record.
  @override
  Future<void> delete(String id) => _db.transaction(() async {
    final micros = _nowMicros();
    final changed =
        await (_db.update(_db.mockExams)..where((e) => e.id.equals(id))).write(
          MockExamsCompanion(
            deleted: const Value(true),
            clientModifiedAt: Value(micros),
          ),
        );
    if (changed == 0) {
      return;
    }
    await _outbox.enqueue(
      entityType: mockExamEntity,
      entityId: id,
      operation: 'delete',
      payload: {'modifiedAt': MockExamRecords.isoMicros(micros)},
    );
  });

  @override
  Future<({String? id, String? name})?> lastSource() async {
    final row =
        await (_db.select(_db.mockExams)
              ..where((e) => e.deleted.equals(false))
              ..orderBy([(e) => OrderingTerm.desc(e.clientModifiedAt)])
              ..limit(1))
            .getSingleOrNull();
    return row == null ? null : (id: row.sourceId, name: row.sourceName);
  }

  int _nowMicros() => _now().toUtc().microsecondsSinceEpoch;
}
