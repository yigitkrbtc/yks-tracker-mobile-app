/// Optional, self-reported; used only as a filter (not in the analysis).
enum Difficulty {
  easy('Easy', 'Kolay'),
  medium('Medium', 'Orta'),
  hard('Hard', 'Zor');

  const Difficulty(this.code, this.label);

  final String code;
  final String label;

  static Difficulty? fromCode(String? code) =>
      // An unknown code (a newer server) reads as unset rather than failing the sync.
      values.where((d) => d.code == code).firstOrNull;
}

/// Where a record stands relative to the server.
enum SyncStatus {
  synced,

  /// Saved on the device; goes to the server when the connection allows.
  pending,

  /// The server refused it; the student must edit or delete it.
  rejected,
}

/// Answers for one topic. Blank is what is left of the questions.
class TopicResult {
  const TopicResult({
    required this.topicId,
    required this.total,
    required this.correct,
    required this.wrong,
    this.mistakeKnowledge,
    this.mistakeAttention,
    this.mistakeTime,
  });

  final String topicId;
  final int total;
  final int correct;
  final int wrong;

  /// Wrong answers by reason; optional.
  final int? mistakeKnowledge;
  final int? mistakeAttention;
  final int? mistakeTime;

  int get blank => total - correct - wrong;

  double get net => TopicTestRules.net(correct, wrong);
}

/// One sitting of a topic test, as stored on the device.
class TopicTest {
  const TopicTest({
    required this.id,
    required this.studiedOn,
    this.sourceId,
    this.sourceName,
    this.isRepeat = false,
    this.difficulty,
    this.durationMinutes,
    this.note,
    required this.results,
    required this.modifiedAt,
    this.syncStatus = SyncStatus.pending,
    this.syncError,
  });

  /// UUID v7, made on the device (ADR-0005).
  final String id;

  /// Calendar day of the test (no time).
  final DateTime studiedOn;

  /// A listed source, or else [sourceName] for one typed by hand.
  final String? sourceId;
  final String? sourceName;
  final bool isRepeat;
  final Difficulty? difficulty;
  final int? durationMinutes;
  final String? note;
  final List<TopicResult> results;

  /// Last edit on this device (UTC); the server keeps the newest edit.
  final DateTime modifiedAt;
  final SyncStatus syncStatus;
  final String? syncError;

  int get total => results.fold(0, (s, r) => s + r.total);
  int get correct => results.fold(0, (s, r) => s + r.correct);
  int get wrong => results.fold(0, (s, r) => s + r.wrong);
  int get blank => results.fold(0, (s, r) => s + r.blank);
  double get net => TopicTestRules.net(correct, wrong);
}

/// The same limits the API enforces, checked before saving so mistakes show while offline.
abstract final class TopicTestRules {
  static const maxTopics = 20;
  static const maxQuestionsPerTopic = 200;
  static const maxDurationMinutes = 600;
  static const noteMaxLength = 500;
  static const sourceNameMaxLength = 100;
  static const maxDaysBack = 365;

  /// Four wrong answers cancel one right one.
  static double net(int correct, int wrong) => correct - wrong / 4;

  /// Today in Türkiye (UTC+3 all year), as a date.
  static DateTime todayInTurkey(DateTime now) {
    final local = now.toUtc().add(const Duration(hours: 3));
    return DateTime.utc(local.year, local.month, local.day);
  }

  static DateTime earliestDate(DateTime now) =>
      todayInTurkey(now).subtract(const Duration(days: maxDaysBack));

  /// Problems with [test], in Turkish; empty when it may be saved.
  static List<String> validate(TopicTest test, DateTime now) {
    final errors = <String>[];
    final day = DateTime.utc(
      test.studiedOn.year,
      test.studiedOn.month,
      test.studiedOn.day,
    );
    if (day.isAfter(todayInTurkey(now)) || day.isBefore(earliestDate(now))) {
      errors.add('Tarih bugün ile son $maxDaysBack gün arasında olmalı.');
    }
    final hasName = (test.sourceName ?? '').trim().isNotEmpty;
    if ((test.sourceId != null) == hasName) {
      errors.add('Listeden bir kaynak seç ya da kaynağın adını yaz.');
    }
    if ((test.sourceName ?? '').trim().length > sourceNameMaxLength) {
      errors.add('Kaynak adı en çok $sourceNameMaxLength karakter olabilir.');
    }
    if (test.results.isEmpty || test.results.length > maxTopics) {
      errors.add('Bir testte 1 ile $maxTopics arasında konu olur.');
    }
    if (test.results.map((r) => r.topicId).toSet().length !=
        test.results.length) {
      errors.add('Bir konu aynı testte iki kez olamaz.');
    }
    for (final r in test.results) {
      if (r.total < 1 || r.total > maxQuestionsPerTopic) {
        errors.add('Soru sayısı 1 ile $maxQuestionsPerTopic arasında olmalı.');
      } else if (r.correct < 0 || r.wrong < 0 || r.blank < 0) {
        errors.add('Doğru ve yanlış toplamı soru sayısını aşamaz.');
      }
      final reasons =
          (r.mistakeKnowledge ?? 0) +
          (r.mistakeAttention ?? 0) +
          (r.mistakeTime ?? 0);
      if (reasons > r.wrong) {
        errors.add('Yanlış nedenlerinin toplamı yanlış sayısını aşamaz.');
      }
    }
    final duration = test.durationMinutes;
    if (duration != null && (duration < 1 || duration > maxDurationMinutes)) {
      errors.add('Süre 1 ile $maxDurationMinutes dakika arasında olmalı.');
    }
    if ((test.note ?? '').length > noteMaxLength) {
      errors.add('Not en çok $noteMaxLength karakter olabilir.');
    }
    return errors.toSet().toList();
  }
}
