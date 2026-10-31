/// The server's analysis (S5, ADR-0016). Numbers come computed; the app never recomputes them.
library;

/// M2 confidence; [none] means no record yet (grey, not "failed").
enum ConfidenceLevel {
  none('None'),
  veryLow('VeryLow'),
  preliminary('Preliminary'),
  reasonable('Reasonable'),
  reliable('Reliable');

  const ConfidenceLevel(this.code);

  final String code;

  static ConfidenceLevel fromCode(String code) =>
      values.firstWhere((v) => v.code == code, orElse: () => none);
}

/// Weak/strong verdict, withheld ([insufficient]) until there are enough questions and records.
enum MasteryBand {
  none('None'),
  insufficient('Insufficient'),
  weak('Weak'),
  developing('Developing'),
  strong('Strong');

  const MasteryBand(this.code);

  final String code;

  static MasteryBand fromCode(String code) =>
      values.firstWhere((v) => v.code == code, orElse: () => none);
}

enum TrendLabel {
  none('None'),
  insufficient('Insufficient'),
  improving('Improving'),
  stable('Stable'),
  declining('Declining');

  const TrendLabel(this.code);

  final String code;

  static TrendLabel fromCode(String code) =>
      values.firstWhere((v) => v.code == code, orElse: () => none);
}

double? _d(Object? v) => (v as num?)?.toDouble();

class Trend {
  const Trend({required this.label, this.probabilityUp, this.difference});

  final TrendLabel label;

  /// P(recent accuracy > previous accuracy).
  final double? probabilityUp;
  final double? difference;

  factory Trend.fromJson(Map<String, dynamic> j) => Trend(
    label: TrendLabel.fromCode(j['label'] as String),
    probabilityUp: _d(j['probabilityUp']),
    difference: _d(j['difference']),
  );

  Map<String, Object?> toJson() => {
    'label': label.code,
    'probabilityUp': probabilityUp,
    'difference': difference,
  };
}

/// M7 lite: q-weighted mastery of a test or subject.
class AggregateStatus {
  const AggregateStatus({
    required this.code,
    required this.isActive,
    required this.mastery,
    required this.masteryLow,
    required this.masteryHigh,
    required this.coverage,
    required this.studiedTopics,
    required this.topicCount,
  });

  final String code;

  /// Part of the student's score types.
  final bool isActive;
  final double mastery;
  final double masteryLow;
  final double masteryHigh;

  /// Share of the exam weight whose topics have enough data.
  final double coverage;
  final int studiedTopics;
  final int topicCount;

  factory AggregateStatus.fromJson(Map<String, dynamic> j) => AggregateStatus(
    code: j['code'] as String,
    isActive: j['isActive'] as bool,
    mastery: _d(j['mastery'])!,
    masteryLow: _d(j['masteryLow'])!,
    masteryHigh: _d(j['masteryHigh'])!,
    coverage: _d(j['coverage'])!,
    studiedTopics: j['studiedTopics'] as int,
    topicCount: j['topicCount'] as int,
  );

  Map<String, Object?> toJson() => {
    'code': code,
    'isActive': isActive,
    'mastery': mastery,
    'masteryLow': masteryLow,
    'masteryHigh': masteryHigh,
    'coverage': coverage,
    'studiedTopics': studiedTopics,
    'topicCount': topicCount,
  };
}

/// M6: a topic worth working on next, and why.
class PriorityTopic {
  const PriorityTopic({
    required this.topicId,
    required this.priority,
    required this.mastery,
    required this.expectedQuestions,
    required this.gain,
    required this.mockBoost,
  });

  final String topicId;
  final double priority;
  final double mastery;
  final double expectedQuestions;

  /// Expected net gain from reaching the target mastery.
  final double gain;

  /// Extra weight from mock exam misses.
  final double mockBoost;

  factory PriorityTopic.fromJson(Map<String, dynamic> j) => PriorityTopic(
    topicId: j['topicId'] as String,
    priority: _d(j['priority'])!,
    mastery: _d(j['mastery'])!,
    expectedQuestions: _d(j['expectedQuestions'])!,
    gain: _d(j['gain'])!,
    mockBoost: _d(j['mockBoost'])!,
  );

  Map<String, Object?> toJson() => {
    'topicId': topicId,
    'priority': priority,
    'mastery': mastery,
    'expectedQuestions': expectedQuestions,
    'gain': gain,
    'mockBoost': mockBoost,
  };
}

/// M11a/b: the student's form in one mock exam section (80 % intervals).
class SectionForm {
  const SectionForm({
    required this.code,
    required this.exams,
    required this.isSufficient,
    required this.accuracy,
    required this.accuracyLow,
    required this.accuracyHigh,
    required this.markingRate,
    required this.expectedNet,
    required this.expectedNetLow,
    required this.expectedNetHigh,
    required this.trend,
    this.practiceGap,
    required this.gapWarning,
  });

  final String code;
  final int exams;
  final bool isSufficient;
  final double accuracy;
  final double accuracyLow;
  final double accuracyHigh;
  final double markingRate;
  final double expectedNet;
  final double expectedNetLow;
  final double expectedNetHigh;
  final Trend trend;

  /// Topic-test mastery minus mock accuracy; null when either side lacks data.
  final double? practiceGap;
  final bool gapWarning;

  factory SectionForm.fromJson(Map<String, dynamic> j) => SectionForm(
    code: j['code'] as String,
    exams: j['exams'] as int,
    isSufficient: j['isSufficient'] as bool,
    accuracy: _d(j['accuracy'])!,
    accuracyLow: _d(j['accuracyLow'])!,
    accuracyHigh: _d(j['accuracyHigh'])!,
    markingRate: _d(j['markingRate'])!,
    expectedNet: _d(j['expectedNet'])!,
    expectedNetLow: _d(j['expectedNetLow'])!,
    expectedNetHigh: _d(j['expectedNetHigh'])!,
    trend: Trend.fromJson(j['trend'] as Map<String, dynamic>),
    practiceGap: _d(j['practiceGap']),
    gapWarning: j['gapWarning'] as bool,
  );

  Map<String, Object?> toJson() => {
    'code': code,
    'exams': exams,
    'isSufficient': isSufficient,
    'accuracy': accuracy,
    'accuracyLow': accuracyLow,
    'accuracyHigh': accuracyHigh,
    'markingRate': markingRate,
    'expectedNet': expectedNet,
    'expectedNetLow': expectedNetLow,
    'expectedNetHigh': expectedNetHigh,
    'trend': trend.toJson(),
    'practiceGap': practiceGap,
    'gapWarning': gapWarning,
  };
}

/// Compact status of a topic with data, for list badges.
class TopicStatus {
  const TopicStatus({
    required this.topicId,
    this.mastery,
    this.masteryLow,
    this.masteryHigh,
    required this.confidenceLevel,
    required this.band,
    required this.trend,
  });

  final String topicId;
  final double? mastery;
  final double? masteryLow;
  final double? masteryHigh;
  final ConfidenceLevel confidenceLevel;
  final MasteryBand band;
  final TrendLabel trend;

  factory TopicStatus.fromJson(Map<String, dynamic> j) => TopicStatus(
    topicId: j['topicId'] as String,
    mastery: _d(j['mastery']),
    masteryLow: _d(j['masteryLow']),
    masteryHigh: _d(j['masteryHigh']),
    confidenceLevel: ConfidenceLevel.fromCode(j['confidenceLevel'] as String),
    band: MasteryBand.fromCode(j['band'] as String),
    trend: TrendLabel.fromCode(j['trend'] as String),
  );

  Map<String, Object?> toJson() => {
    'topicId': topicId,
    'mastery': mastery,
    'masteryLow': masteryLow,
    'masteryHigh': masteryHigh,
    'confidenceLevel': confidenceLevel.code,
    'band': band.code,
    'trend': trend.code,
  };
}

/// The analysis panel.
class AnalysisOverview {
  const AnalysisOverview({
    required this.etag,
    required this.computedAt,
    required this.ruleSetVersion,
    required this.tests,
    required this.subjects,
    required this.priorities,
    required this.lowDataTopics,
    required this.sections,
    required this.topics,
  });

  final String etag;
  final DateTime computedAt;
  final String ruleSetVersion;
  final List<AggregateStatus> tests;
  final List<AggregateStatus> subjects;
  final List<PriorityTopic> priorities;

  /// Heavy topics without enough data to judge yet.
  final List<String> lowDataTopics;
  final List<SectionForm> sections;
  final List<TopicStatus> topics;

  bool get hasData => topics.isNotEmpty || sections.isNotEmpty;

  Map<String, TopicStatus> get topicsById => {
    for (final t in topics) t.topicId: t,
  };

  factory AnalysisOverview.fromJson(Map<String, dynamic> j) => AnalysisOverview(
    etag: j['eTag'] as String,
    computedAt: DateTime.parse(j['computedAt'] as String),
    ruleSetVersion: j['ruleSetVersion'] as String,
    tests: [
      for (final t in j['tests'] as List<dynamic>)
        AggregateStatus.fromJson(t as Map<String, dynamic>),
    ],
    subjects: [
      for (final s in j['subjects'] as List<dynamic>)
        AggregateStatus.fromJson(s as Map<String, dynamic>),
    ],
    priorities: [
      for (final p in j['priorities'] as List<dynamic>)
        PriorityTopic.fromJson(p as Map<String, dynamic>),
    ],
    lowDataTopics: (j['lowDataTopics'] as List<dynamic>).cast<String>(),
    sections: [
      for (final s in j['sections'] as List<dynamic>)
        SectionForm.fromJson(s as Map<String, dynamic>),
    ],
    topics: [
      for (final t in j['topics'] as List<dynamic>)
        TopicStatus.fromJson(t as Map<String, dynamic>),
    ],
  );

  Map<String, Object?> toJson() => {
    'eTag': etag,
    'computedAt': computedAt.toIso8601String(),
    'ruleSetVersion': ruleSetVersion,
    'tests': [for (final t in tests) t.toJson()],
    'subjects': [for (final s in subjects) s.toJson()],
    'priorities': [for (final p in priorities) p.toJson()],
    'lowDataTopics': lowDataTopics,
    'sections': [for (final s in sections) s.toJson()],
    'topics': [for (final t in topics) t.toJson()],
  };
}

/// M11c: wrong and blank answers tagged to the topic in fully tagged mock sections.
class MockSignal {
  const MockSignal({
    required this.exposure,
    required this.missed,
    required this.wrong,
    required this.blank,
    required this.exams,
    required this.rate,
    required this.subjectRate,
    required this.shown,
  });

  /// Questions the topic was expected to get in those exams.
  final double exposure;
  final int missed;
  final int wrong;
  final int blank;
  final int exams;
  final double rate;
  final double subjectRate;
  final bool shown;

  factory MockSignal.fromJson(Map<String, dynamic> j) => MockSignal(
    exposure: _d(j['exposure'])!,
    missed: j['missed'] as int,
    wrong: j['wrong'] as int,
    blank: j['blank'] as int,
    exams: j['exams'] as int,
    rate: _d(j['rate'])!,
    subjectRate: _d(j['subjectRate'])!,
    shown: j['shown'] as bool,
  );
}

class RecentRecord {
  const RecentRecord({
    required this.date,
    required this.total,
    required this.correct,
    required this.wrong,
    required this.blank,
    required this.isRepeat,
  });

  final DateTime date;
  final int total;
  final int correct;
  final int wrong;
  final int blank;
  final bool isRepeat;

  double get net => correct - wrong / 4;

  factory RecentRecord.fromJson(Map<String, dynamic> j) => RecentRecord(
    date: DateTime.parse(j['date'] as String),
    total: j['total'] as int,
    correct: j['correct'] as int,
    wrong: j['wrong'] as int,
    blank: j['blank'] as int,
    isRepeat: j['isRepeat'] as bool,
  );
}

/// The topic card.
class TopicAnalysis {
  const TopicAnalysis({
    required this.topicId,
    required this.computedAt,
    this.mastery,
    this.masteryLow,
    this.masteryHigh,
    this.markingRate,
    required this.optimistic,
    this.expectedNetPerQuestion,
    this.confidence,
    required this.confidenceLevel,
    this.questionsToNextLevel,
    required this.band,
    required this.trend,
    this.priority,
    required this.records,
    required this.questions,
    required this.correct,
    required this.wrong,
    required this.blank,
    this.mockSignal,
    required this.recent,
  });

  final String topicId;
  final DateTime computedAt;
  final double? mastery;
  final double? masteryLow;
  final double? masteryHigh;
  final double? markingRate;

  /// Many blanks: accuracy alone flatters the student.
  final bool optimistic;
  final double? expectedNetPerQuestion;
  final double? confidence;
  final ConfidenceLevel confidenceLevel;

  /// Fresh questions that would lift the confidence one level.
  final int? questionsToNextLevel;
  final MasteryBand band;
  final Trend trend;
  final double? priority;
  final int records;
  final int questions;
  final int correct;
  final int wrong;
  final int blank;
  final MockSignal? mockSignal;
  final List<RecentRecord> recent;

  bool get hasData => records > 0;

  factory TopicAnalysis.fromJson(Map<String, dynamic> j) {
    final a = j['analysis'] as Map<String, dynamic>;
    final totals = a['totals'] as Map<String, dynamic>;
    return TopicAnalysis(
      topicId: j['topicId'] as String,
      computedAt: DateTime.parse(j['computedAt'] as String),
      mastery: _d(a['mastery']),
      masteryLow: _d(a['masteryLow']),
      masteryHigh: _d(a['masteryHigh']),
      markingRate: _d(a['markingRate']),
      optimistic: a['optimistic'] as bool,
      expectedNetPerQuestion: _d(a['expectedNetPerQuestion']),
      confidence: _d(a['confidence']),
      confidenceLevel: ConfidenceLevel.fromCode(a['confidenceLevel'] as String),
      questionsToNextLevel: a['questionsToNextLevel'] as int?,
      band: MasteryBand.fromCode(a['band'] as String),
      trend: Trend.fromJson(a['trend'] as Map<String, dynamic>),
      priority: _d(a['priority']),
      records: totals['records'] as int,
      questions: totals['questions'] as int,
      correct: totals['correct'] as int,
      wrong: totals['wrong'] as int,
      blank: totals['blank'] as int,
      mockSignal: a['mockSignal'] == null
          ? null
          : MockSignal.fromJson(a['mockSignal'] as Map<String, dynamic>),
      recent: [
        for (final r in a['recent'] as List<dynamic>)
          RecentRecord.fromJson(r as Map<String, dynamic>),
      ],
    );
  }
}
