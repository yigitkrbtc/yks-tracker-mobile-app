import 'package:drift/drift.dart';

part 'app_database.g.dart';

/// Topic tests as last written on this device; the screens read only these rows (offline-first, ADR-0005).
@DataClassName('TopicTestRow')
class TopicTests extends Table {
  TextColumn get id => text()();
  TextColumn get studiedOn => text()(); // yyyy-MM-dd
  TextColumn get sourceId => text().nullable()();
  TextColumn get sourceName => text().nullable()();
  BoolColumn get isRepeat => boolean()();
  TextColumn get difficulty => text().nullable()();
  IntColumn get durationMinutes => integer().nullable()();
  TextColumn get note => text().nullable()();

  /// Microseconds since epoch, UTC; the server keeps the newest edit by this time.
  IntColumn get clientModifiedAt => integer()();

  /// Hidden from the screens, waiting for the delete to reach the server.
  BoolColumn get deleted => boolean().withDefault(const Constant(false))();

  /// Position in the server's change feed; 0 until the server has confirmed it.
  IntColumn get changeSeq => integer().withDefault(const Constant(0))();

  @override
  Set<Column> get primaryKey => {id};
}

@DataClassName('TopicResultRow')
class TopicTestResults extends Table {
  TextColumn get testId =>
      text().references(TopicTests, #id, onDelete: KeyAction.cascade)();
  TextColumn get topicId => text()();
  IntColumn get total => integer()();
  IntColumn get correct => integer()();
  IntColumn get wrong => integer()();
  IntColumn get blank => integer()();
  IntColumn get mistakeKnowledge => integer().nullable()();
  IntColumn get mistakeAttention => integer().nullable()();
  IntColumn get mistakeTime => integer().nullable()();

  @override
  Set<Column> get primaryKey => {testId, topicId};
}

/// Mock exams as last written on this device (S4, ADR-0015); same sync rules as topic tests.
@DataClassName('MockExamRow')
class MockExams extends Table {
  TextColumn get id => text()();

  /// `Tyt`, `Ayt` or `Branch` (API codes).
  TextColumn get kind => text()();
  TextColumn get takenOn => text()(); // yyyy-MM-dd
  TextColumn get sourceId => text().nullable()();
  TextColumn get sourceName => text().nullable()();
  TextColumn get title => text().nullable()();
  IntColumn get durationMinutes => integer().nullable()();
  TextColumn get note => text().nullable()();

  /// Microseconds since epoch, UTC; the server keeps the newest edit by this time.
  IntColumn get clientModifiedAt => integer()();

  /// Hidden from the screens, waiting for the delete to reach the server.
  BoolColumn get deleted => boolean().withDefault(const Constant(false))();

  /// Position in the server's change feed; 0 until the server has confirmed it.
  IntColumn get changeSeq => integer().withDefault(const Constant(0))();

  @override
  Set<Column> get primaryKey => {id};
}

/// Solved sections only; an unsolved section has no row.
@DataClassName('MockExamSectionRow')
class MockExamSections extends Table {
  TextColumn get examId =>
      text().references(MockExams, #id, onDelete: KeyAction.cascade)();
  TextColumn get sectionCode => text()();
  IntColumn get questionCount => integer()();
  IntColumn get correct => integer()();
  IntColumn get wrong => integer()();

  @override
  Set<Column> get primaryKey => {examId, sectionCode};
}

@DataClassName('MockExamTopicMarkRow')
class MockExamTopicMarks extends Table {
  TextColumn get examId =>
      text().references(MockExams, #id, onDelete: KeyAction.cascade)();
  TextColumn get sectionCode => text()();
  TextColumn get topicId => text()();
  IntColumn get wrong => integer()();
  IntColumn get blank => integer()();
  IntColumn get mistakeKnowledge => integer().nullable()();
  IntColumn get mistakeAttention => integer().nullable()();
  IntColumn get mistakeTime => integer().nullable()();

  @override
  Set<Column> get primaryKey => {examId, sectionCode, topicId};
}

/// Local writes waiting for the server, at most one per record (a newer write replaces the queued one).
@DataClassName('OutboxRow')
class Outbox extends Table {
  IntColumn get id => integer().autoIncrement()();
  TextColumn get entityType => text()();
  TextColumn get entityId => text()();

  /// `put` or `delete`.
  TextColumn get operation => text()();
  TextColumn get payload => text().nullable()(); // JSON body of the request
  /// Bumped when a newer write replaces the queued one, so an in-flight push cannot remove it.
  IntColumn get revision => integer().withDefault(const Constant(0))();

  /// The server refused it (400/404); it waits for the student to fix or drop the record.
  BoolColumn get rejected => boolean().withDefault(const Constant(false))();
  TextColumn get lastError => text().nullable()();

  @override
  List<Set<Column>> get uniqueKeys => [
    {entityType, entityId},
  ];
}

@DataClassName('SyncCursorRow')
class SyncCursors extends Table {
  TextColumn get stream => text()();
  IntColumn get cursor => integer()();

  @override
  Set<Column> get primaryKey => {stream};
}

/// Small documents kept for offline start: the curriculum, the study profile, the data owner.
@DataClassName('CacheEntryRow')
class CacheEntries extends Table {
  TextColumn get key => text()();
  TextColumn get value => text()();

  @override
  Set<Column> get primaryKey => {key};
}

/// The device's single SQLite database. Schema lives in core so features share one transaction for a record
/// and its outbox entry; features reach it only from their `data` layer.
@DriftDatabase(
  tables: [
    TopicTests,
    TopicTestResults,
    MockExams,
    MockExamSections,
    MockExamTopicMarks,
    Outbox,
    SyncCursors,
    CacheEntries,
  ],
)
class AppDatabase extends _$AppDatabase {
  AppDatabase(super.executor);

  @override
  int get schemaVersion => 2;

  @override
  MigrationStrategy get migration => MigrationStrategy(
    onUpgrade: (m, from, to) async {
      if (from < 2) {
        // S4: mock exams. Existing topic tests and queued writes stay.
        await m.createTable(mockExams);
        await m.createTable(mockExamSections);
        await m.createTable(mockExamTopicMarks);
      }
    },
    beforeOpen: (details) async {
      await customStatement('PRAGMA foreign_keys = ON');
    },
  );

  /// Removes everything this device holds for the signed-in student (sign-out, another account).
  Future<void> wipe() => transaction(() async {
    for (final table in allTables) {
      await delete(table).go();
    }
  });
}
