/// Why an item is in the plan (ADR-0017). Unknown kinds from a newer server read as [other].
enum PlanItemKind {
  tagMockExam('TagMockExam'),
  focus('Focus'),
  decline('Decline'),
  collectData('CollectData'),
  review('Review'),
  practiceGap('PracticeGap'),
  firstRecord('FirstRecord'),
  other('');

  const PlanItemKind(this.code);

  final String code;

  static PlanItemKind fromCode(String code) =>
      values.firstWhere((k) => k.code == code, orElse: () => other);
}

enum DismissReason {
  notNow('NotNow', 'Bu hafta değil'),
  alreadyKnow('AlreadyKnow', 'Bu konuyu biliyorum');

  const DismissReason(this.code, this.label);

  final String code;
  final String label;
}

class PlanItem {
  const PlanItem({
    required this.key,
    required this.kind,
    this.topicId,
    this.sectionCode,
    this.mockExamId,
    this.targetQuestions,
    required this.title,
    required this.text,
    required this.done,
    required this.completed,
  });

  factory PlanItem.fromJson(Map<String, dynamic> j) => PlanItem(
    key: j['key'] as String,
    kind: PlanItemKind.fromCode(j['kind'] as String),
    topicId: j['topicId'] as String?,
    sectionCode: j['sectionCode'] as String?,
    mockExamId: j['mockExamId'] as String?,
    targetQuestions: j['targetQuestions'] as int?,
    title: j['title'] as String,
    text: j['text'] as String,
    done: j['done'] as int,
    completed: j['completed'] as bool,
  );

  final String key;
  final PlanItemKind kind;
  final String? topicId;
  final String? sectionCode;
  final String? mockExamId;
  final int? targetQuestions;
  final String title;

  /// Evidence, reason and benefit, written by the server.
  final String text;

  /// Questions recorded on the topic this week (as of the last download).
  final int done;
  final bool completed;

  Map<String, Object?> toJson() => {
    'key': key,
    'kind': kind.code,
    'topicId': topicId,
    'sectionCode': sectionCode,
    'mockExamId': mockExamId,
    'targetQuestions': targetQuestions,
    'title': title,
    'text': text,
    'done': done,
    'completed': completed,
  };
}

/// This week's plan, frozen for the week with live progress.
class WeeklyPlan {
  const WeeklyPlan({
    required this.etag,
    required this.weekStart,
    required this.items,
  });

  factory WeeklyPlan.fromJson(Map<String, dynamic> j) => WeeklyPlan(
    etag: j['eTag'] as String,
    weekStart: j['weekStart'] as String,
    items: [
      for (final i in j['items'] as List<dynamic>)
        PlanItem.fromJson(i as Map<String, dynamic>),
    ],
  );

  final String etag;

  /// Monday, `yyyy-MM-dd`.
  final String weekStart;
  final List<PlanItem> items;

  int get completedCount => items.where((i) => i.completed).length;

  Map<String, Object?> toJson() => {
    'eTag': etag,
    'weekStart': weekStart,
    'items': [for (final i in items) i.toJson()],
  };
}
