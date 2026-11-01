/// The student's own mark on a topic (S6, K7). It only changes recommendation wording, never the analysis.
enum ProgressStatus {
  notStarted('NotStarted', 'Henüz çalışmadım'),
  inProgress('InProgress', 'Çalışıyorum'),
  done('Done', 'Bitirdim');

  const ProgressStatus(this.code, this.label);

  /// API value.
  final String code;
  final String label;

  static ProgressStatus? fromCode(String? code) {
    for (final s in values) {
      if (s.code == code) {
        return s;
      }
    }
    return null;
  }
}

/// A topic counted by [CurriculumProgress]: its id and how many questions it brings per exam.
typedef WeightedTopic = ({String id, double expectedQuestions});

/// Share of the student's topics marked done (K8): by count, and weighted by ÖSYM questions per exam.
class CurriculumProgress {
  const CurriculumProgress({
    required this.done,
    required this.inProgress,
    required this.total,
    required this.weightedShare,
  });

  factory CurriculumProgress.of(
    Iterable<WeightedTopic> topics,
    Map<String, ProgressStatus> marks,
  ) {
    var done = 0;
    var inProgress = 0;
    var total = 0;
    var weight = 0.0;
    var doneWeight = 0.0;
    for (final t in topics) {
      total++;
      weight += t.expectedQuestions;
      switch (marks[t.id]) {
        case ProgressStatus.done:
          done++;
          doneWeight += t.expectedQuestions;
        case ProgressStatus.inProgress:
          inProgress++;
        case ProgressStatus.notStarted || null:
          break;
      }
    }
    return CurriculumProgress(
      done: done,
      inProgress: inProgress,
      total: total,
      weightedShare: weight == 0 ? 0 : doneWeight / weight,
    );
  }

  final int done;
  final int inProgress;
  final int total;

  /// Done topics' share of expected questions, 0–1.
  final double weightedShare;

  double get share => total == 0 ? 0 : done / total;
}
