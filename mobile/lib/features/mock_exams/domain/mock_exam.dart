import '../../curriculum/domain/curriculum.dart';
import '../../topic_tests/domain/topic_test.dart' show SyncStatus;

export '../../topic_tests/domain/topic_test.dart' show SyncStatus;

enum MockExamKind {
  tyt('Tyt', 'TYT'),
  ayt('Ayt', 'AYT'),

  /// One section only; its question count may differ from ÖSYM's.
  branch('Branch', 'Branş');

  const MockExamKind(this.code, this.label);

  /// API value.
  final String code;
  final String label;

  static MockExamKind fromCode(String code) =>
      values.firstWhere((k) => k.code == code, orElse: () => tyt);
}

/// How much of a section's lost questions are marked with topics; analysis uses only [full] (M11c).
enum TaggingStatus { none, partial, full }

/// One solved section. Blank is what is left of the questions.
class MockSectionResult {
  const MockSectionResult({
    required this.sectionCode,
    required this.questionCount,
    required this.correct,
    required this.wrong,
  });

  final String sectionCode;
  final int questionCount;
  final int correct;
  final int wrong;

  int get blank => questionCount - correct - wrong;

  /// Questions lost: the ones a student can mark with topics.
  int get lost => wrong + blank;

  double get net => MockExamRules.net(correct, wrong);
}

/// Wrong and blank answers of one section put on one topic.
class TopicMark {
  const TopicMark({
    required this.sectionCode,
    required this.topicId,
    required this.wrong,
    required this.blank,
    this.mistakeKnowledge,
    this.mistakeAttention,
    this.mistakeTime,
  });

  final String sectionCode;
  final String topicId;
  final int wrong;
  final int blank;
  final int? mistakeKnowledge;
  final int? mistakeAttention;
  final int? mistakeTime;
}

/// A mock exam as stored on the device. Only solved sections are listed.
class MockExam {
  const MockExam({
    required this.id,
    required this.kind,
    required this.takenOn,
    this.sourceId,
    this.sourceName,
    this.title,
    this.durationMinutes,
    this.note,
    required this.sections,
    this.marks = const [],
    required this.modifiedAt,
    this.syncStatus = SyncStatus.pending,
    this.syncError,
  });

  /// UUID v7, made on the device (ADR-0005).
  final String id;
  final MockExamKind kind;
  final DateTime takenOn;

  /// A listed publisher, or else [sourceName] typed by hand.
  final String? sourceId;
  final String? sourceName;
  final String? title;
  final int? durationMinutes;
  final String? note;
  final List<MockSectionResult> sections;
  final List<TopicMark> marks;
  final DateTime modifiedAt;
  final SyncStatus syncStatus;
  final String? syncError;

  int get questionCount => sections.fold(0, (s, x) => s + x.questionCount);
  int get correct => sections.fold(0, (s, x) => s + x.correct);
  int get wrong => sections.fold(0, (s, x) => s + x.wrong);
  int get blank => sections.fold(0, (s, x) => s + x.blank);
  double get net => MockExamRules.net(correct, wrong);

  List<TopicMark> marksOf(String sectionCode) => [
    for (final m in marks)
      if (m.sectionCode == sectionCode) m,
  ];

  TaggingStatus taggingOf(MockSectionResult section) {
    final tagged = marksOf(section.sectionCode)
        .fold(0, (s, m) => s + m.wrong + m.blank);
    return tagged == section.lost
        ? TaggingStatus.full
        : tagged == 0
        ? TaggingStatus.none
        : TaggingStatus.partial;
  }

  /// Some lost questions are not marked yet (shown as a hint in the list).
  bool get needsTagging =>
      sections.any((s) => taggingOf(s) != TaggingStatus.full);

  MockExam copyWith({List<TopicMark>? marks}) => MockExam(
    id: id,
    kind: kind,
    takenOn: takenOn,
    sourceId: sourceId,
    sourceName: sourceName,
    title: title,
    durationMinutes: durationMinutes,
    note: note,
    sections: sections,
    marks: marks ?? this.marks,
    modifiedAt: modifiedAt,
    syncStatus: syncStatus,
    syncError: syncError,
  );
}

/// The same rules the API enforces, checked before saving so mistakes show while offline.
abstract final class MockExamRules {
  static const maxBranchQuestions = 80;
  static const titleMaxLength = 100;
  static const maxDurationMinutes = 600;
  static const noteMaxLength = 500;
  static const sourceNameMaxLength = 100;
  static const maxDaysBack = 365;

  static double net(int correct, int wrong) => correct - wrong / 4;

  static DateTime todayInTurkey(DateTime now) {
    final local = now.toUtc().add(const Duration(hours: 3));
    return DateTime.utc(local.year, local.month, local.day);
  }

  static DateTime earliestDate(DateTime now) =>
      todayInTurkey(now).subtract(const Duration(days: maxDaysBack));

  /// Sections offered for [kind]: the session's sections (TYT or AYT), every section for a branch exam.
  static List<MockSection> sectionsFor(MockExamKind kind, Curriculum c) => [
    for (final s in c.mockSections)
      if (kind == MockExamKind.branch ||
          c.sessionOfTest(s.testCode) ==
              (kind == MockExamKind.tyt ? ExamSession.tyt : ExamSession.ayt))
        s,
  ];

  /// Problems with [exam], in Turkish; empty when it may be saved.
  static List<String> validate(MockExam exam, Curriculum c, DateTime now) {
    final errors = <String>[];
    final day = DateTime.utc(
      exam.takenOn.year,
      exam.takenOn.month,
      exam.takenOn.day,
    );
    if (day.isAfter(todayInTurkey(now)) || day.isBefore(earliestDate(now))) {
      errors.add('Tarih bugün ile son $maxDaysBack gün arasında olmalı.');
    }
    final hasName = (exam.sourceName ?? '').trim().isNotEmpty;
    if ((exam.sourceId != null) == hasName) {
      errors.add('Listeden bir yayınevi seç ya da adını yaz.');
    }
    if ((exam.sourceName ?? '').trim().length > sourceNameMaxLength) {
      errors.add('Yayınevi adı en çok $sourceNameMaxLength karakter olabilir.');
    }
    if ((exam.title ?? '').trim().length > titleMaxLength) {
      errors.add('Deneme adı en çok $titleMaxLength karakter olabilir.');
    }
    if (exam.sections.isEmpty) {
      errors.add('En az bir bölümün doğru ve yanlışını gir.');
    }
    if (exam.kind == MockExamKind.branch && exam.sections.length > 1) {
      errors.add('Branş denemesi tek bölümdür.');
    }

    final template = {for (final s in c.mockSections) s.code: s};
    final codes = {for (final s in exam.sections) s.sectionCode};
    for (final s in exam.sections) {
      final def = template[s.sectionCode];
      if (def == null) {
        errors.add('Bölümlerden biri sınav yapısında yok.');
        continue;
      }
      if (exam.kind == MockExamKind.branch) {
        if (s.questionCount < 1 || s.questionCount > maxBranchQuestions) {
          errors.add('Soru sayısı 1 ile $maxBranchQuestions arasında olmalı.');
        }
      } else if (s.questionCount != def.questionCount) {
        errors.add('${def.name} bölümü ${def.questionCount} sorudur.');
      }
      if (s.correct < 0 || s.wrong < 0 || s.blank < 0) {
        errors.add('${def.name}: doğru + yanlış soru sayısını aşamaz.');
      }
      if ((def.parentCode != null && codes.contains(def.parentCode)) ||
          (def.alternativeOf != null && codes.contains(def.alternativeOf))) {
        errors.add(
          'Bir blok ya toplam olarak ya da dersleriyle girilir; '
          'Din ve yerine çözülen felsefe birlikte olmaz.',
        );
      }
    }

    for (final s in exam.sections) {
      final marks = exam.marksOf(s.sectionCode);
      final wrong = marks.fold(0, (a, m) => a + m.wrong);
      final blank = marks.fold(0, (a, m) => a + m.blank);
      if (wrong > s.wrong || blank > s.blank) {
        errors.add(
          'Etiketlenen yanlış ve boşlar bölümün yanlış ve boş sayısını aşamaz.',
        );
      }
    }
    for (final m in exam.marks) {
      final reasons =
          (m.mistakeKnowledge ?? 0) +
          (m.mistakeAttention ?? 0) +
          (m.mistakeTime ?? 0);
      if (m.wrong < 0 || m.blank < 0 || m.wrong + m.blank == 0) {
        errors.add('Etiket en az bir yanlış ya da boş içermeli.');
      }
      if (reasons > m.wrong) {
        errors.add('Yanlış nedenlerinin toplamı yanlış sayısını aşamaz.');
      }
      if (!codes.contains(m.sectionCode)) {
        errors.add('Etiketlenen bölüm denemede yok.');
      }
    }

    final duration = exam.durationMinutes;
    if (duration != null && (duration < 1 || duration > maxDurationMinutes)) {
      errors.add('Süre 1 ile $maxDurationMinutes dakika arasında olmalı.');
    }
    if ((exam.note ?? '').length > noteMaxLength) {
      errors.add('Not en çok $noteMaxLength karakter olabilir.');
    }
    return errors.toSet().toList();
  }
}
