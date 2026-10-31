/// How often a topic is asked, from our tagging of past ÖSYM booklets (not an official figure).
enum WeightTier {
  rare('Rare'),
  low('Low'),
  medium('Medium'),
  high('High');

  const WeightTier(this.code);

  final String code;

  static WeightTier fromCode(String code) =>
      values.firstWhere((t) => t.code == code);
}

enum ExamSession { tyt, ayt }

class Topic {
  const Topic({
    required this.id,
    required this.code,
    required this.name,
    required this.subtopics,
    required this.gradeMin,
    required this.gradeMax,
    required this.expectedQuestions,
    required this.tier,
  });

  final String id;
  final String code;
  final String name;
  final List<String> subtopics;
  final int gradeMin;
  final int gradeMax;

  /// Average questions per exam over 2022–2026.
  final double expectedQuestions;
  final WeightTier tier;

  /// Not asked at all in the years the average covers.
  bool get notAskedRecently => expectedQuestions == 0;

  String get gradeLabel =>
      gradeMin == gradeMax ? '$gradeMin. sınıf' : '$gradeMin–$gradeMax. sınıf';

  Map<String, Object?> toJson() => {
    'id': id,
    'code': code,
    'name': name,
    'subtopics': subtopics,
    'gradeMin': gradeMin,
    'gradeMax': gradeMax,
    'expectedQuestions': expectedQuestions,
    'tier': tier.code,
  };

  factory Topic.fromJson(Map<String, dynamic> json) => Topic(
    id: json['id'] as String,
    code: json['code'] as String,
    name: json['name'] as String,
    subtopics: (json['subtopics'] as List<dynamic>).cast<String>(),
    gradeMin: json['gradeMin'] as int,
    gradeMax: json['gradeMax'] as int,
    expectedQuestions: (json['expectedQuestions'] as num).toDouble(),
    tier: WeightTier.fromCode(json['tier'] as String),
  );
}

class Subject {
  const Subject({
    required this.code,
    required this.session,
    required this.name,
    required this.testCodes,
    required this.topics,
  });

  final String code;
  final ExamSession session;
  final String name;

  /// Tests this subject is asked in; AYT history and geography are in two.
  final List<String> testCodes;
  final List<Topic> topics;

  Map<String, Object?> toJson() => {
    'code': code,
    'session': session == ExamSession.tyt ? 'TYT' : 'AYT',
    'name': name,
    'testCodes': testCodes,
    'topics': [for (final t in topics) t.toJson()],
  };

  factory Subject.fromJson(Map<String, dynamic> json) => Subject(
    code: json['code'] as String,
    session: _session(json['session'] as String),
    name: json['name'] as String,
    testCodes: (json['testCodes'] as List<dynamic>).cast<String>(),
    topics: [
      for (final t in json['topics'] as List<dynamic>)
        Topic.fromJson(t as Map<String, dynamic>),
    ],
  );
}

class ScoreTypeTests {
  const ScoreTypeTests({required this.code, required this.testCodes});

  final String code;
  final List<String> testCodes;
}

enum SourceKind {
  publisher('Publisher'),
  official('Official'),
  institution('Institution');

  const SourceKind(this.code);

  final String code;

  static SourceKind fromCode(String code) =>
      values.firstWhere((k) => k.code == code, orElse: () => publisher);
}

/// Where questions come from (a publisher, ÖSYM past papers); picked when recording a test.
class QuestionSource {
  const QuestionSource({
    required this.id,
    required this.code,
    required this.name,
    required this.kind,
  });

  final String id;
  final String code;
  final String name;
  final SourceKind kind;

  Map<String, Object?> toJson() => {
    'id': id,
    'code': code,
    'name': name,
    'kind': kind.code,
  };

  factory QuestionSource.fromJson(Map<String, dynamic> json) => QuestionSource(
    id: json['id'] as String,
    code: json['code'] as String,
    name: json['name'] as String,
    kind: SourceKind.fromCode(json['kind'] as String),
  );
}

/// A line of a mock exam's score sheet (e.g. TYT Sosyal → Tarih 5). Each test has one root section; a block
/// (Sosyal, Fen, AYT TDE-SB1 and SB2) also has a child per subject and is entered either as its total or by
/// its children. An alternative (philosophy instead of religion) replaces [alternativeOf].
class MockSection {
  const MockSection({
    required this.code,
    required this.testCode,
    this.subjectCode,
    required this.name,
    required this.questionCount,
    this.parentCode,
    this.alternativeOf,
  });

  final String code;
  final String testCode;

  /// Null when the section spans every subject of its test (a block, or Matematik with geometry).
  final String? subjectCode;
  final String name;
  final int questionCount;
  final String? parentCode;
  final String? alternativeOf;

  bool get isRoot => parentCode == null;

  Map<String, Object?> toJson() => {
    'code': code,
    'testCode': testCode,
    'subjectCode': subjectCode,
    'name': name,
    'questionCount': questionCount,
    'parentCode': parentCode,
    'alternativeOf': alternativeOf,
  };

  factory MockSection.fromJson(Map<String, dynamic> json) => MockSection(
    code: json['code'] as String,
    testCode: json['testCode'] as String,
    subjectCode: json['subjectCode'] as String?,
    name: json['name'] as String,
    questionCount: json['questionCount'] as int,
    parentCode: json['parentCode'] as String?,
    alternativeOf: json['alternativeOf'] as String?,
  );
}

/// The published curriculum. The server decides which tests each score type covers; the client only filters.
class Curriculum {
  const Curriculum({
    required this.version,
    required this.examYear,
    required this.scoreTypes,
    required this.subjects,
    this.sources = const [],
    this.mockSections = const [],
  });

  /// Content hash; sent back as the ETag to skip unchanged downloads.
  final String version;
  final int examYear;
  final List<ScoreTypeTests> scoreTypes;
  final List<Subject> subjects;
  final List<QuestionSource> sources;

  /// The mock exam score sheet template, in display order.
  final List<MockSection> mockSections;

  /// Subjects a student of the given score type codes studies, in curriculum order.
  List<Subject> subjectsFor(Iterable<String> scoreTypeCodes) {
    final codes = scoreTypeCodes.toSet();
    final tests = {
      for (final s in scoreTypes)
        if (codes.contains(s.code)) ...s.testCodes,
    };
    return [
      for (final subject in subjects)
        if (subject.testCodes.any(tests.contains)) subject,
    ];
  }

  /// Every topic by id, retired ones excluded (the server lists only active topics).
  Map<String, Topic> get topicsById => {
    for (final s in subjects)
      for (final t in s.topics) t.id: t,
  };

  /// Session of a test code (TYT-… or AYT-…).
  ExamSession sessionOfTest(String testCode) =>
      testCode.startsWith('AYT') ? ExamSession.ayt : ExamSession.tyt;

  /// Subjects whose topics may be marked in [section]: its own subject, or every subject of its test.
  List<Subject> subjectsOfSection(MockSection section) => [
    for (final s in subjects)
      if (section.subjectCode == null
          ? s.testCodes.contains(section.testCode)
          : s.code == section.subjectCode)
        s,
  ];

  Map<String, Object?> toJson() => {
    'version': version,
    'examYear': examYear,
    'scoreTypes': [
      for (final s in scoreTypes) {'code': s.code, 'testCodes': s.testCodes},
    ],
    'subjects': [for (final s in subjects) s.toJson()],
    'sources': [for (final s in sources) s.toJson()],
    'mockSections': [for (final s in mockSections) s.toJson()],
  };

  factory Curriculum.fromJson(Map<String, dynamic> json) => Curriculum(
    version: json['version'] as String,
    examYear: json['examYear'] as int,
    scoreTypes: [
      for (final s in json['scoreTypes'] as List<dynamic>)
        ScoreTypeTests(
          code: (s as Map<String, dynamic>)['code'] as String,
          testCodes: (s['testCodes'] as List<dynamic>).cast<String>(),
        ),
    ],
    subjects: [
      for (final s in json['subjects'] as List<dynamic>)
        Subject.fromJson(s as Map<String, dynamic>),
    ],
    sources: [
      for (final s in json['sources'] as List<dynamic>? ?? const [])
        QuestionSource.fromJson(s as Map<String, dynamic>),
    ],
    mockSections: [
      for (final s in json['mockSections'] as List<dynamic>? ?? const [])
        MockSection.fromJson(s as Map<String, dynamic>),
    ],
  );
}

ExamSession _session(String value) => switch (value) {
  'TYT' => ExamSession.tyt,
  'AYT' => ExamSession.ayt,
  _ => throw FormatException('Unknown exam session $value'),
};
