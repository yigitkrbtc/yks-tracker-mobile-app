import 'package:nirengi/features/data_export/domain/data_export.dart';
import 'package:nirengi/features/exam_calendar/domain/exam_schedule.dart';
import 'package:nirengi/features/exam_calendar/domain/exam_schedule_repository.dart';
import 'package:nirengi/features/recommendations/domain/recommendation_repository.dart';
import 'package:nirengi/features/recommendations/domain/weekly_plan.dart';

PlanItem planItem(
  String key, {
  PlanItemKind kind = PlanItemKind.focus,
  String? topicId,
  String? mockExamId,
  int? target = 30,
  int done = 0,
  bool completed = false,
}) => PlanItem(
  key: key,
  kind: kind,
  topicId: topicId,
  mockExamId: mockExamId,
  targetQuestions: target,
  title: 'Başlık $key',
  text: 'Gerekçe $key',
  done: done,
  completed: completed,
);

class FakeRecommendationRepository implements RecommendationRepository {
  FakeRecommendationRepository([WeeklyPlan? plan])
    : plan =
          plan ??
          const WeeklyPlan(etag: '"e0"', weekStart: '2026-10-26', items: []);

  WeeklyPlan plan;

  /// Items that take a dismissed item's place, first first.
  final replacements = <PlanItem>[];
  final dismissed = <(String, DismissReason)>[];
  RecommendationException? error;

  @override
  Future<WeeklyPlan?> fetchWeek({String? ifNoneMatch}) async {
    if (error case final e?) {
      throw e;
    }
    return ifNoneMatch == plan.etag ? null : plan;
  }

  @override
  Future<WeeklyPlan> dismiss(String itemKey, DismissReason reason) async {
    if (error case final e?) {
      throw e;
    }
    dismissed.add((itemKey, reason));
    final items = [
      for (final i in plan.items)
        if (i.key != itemKey) i,
      if (replacements.isNotEmpty) replacements.removeAt(0),
    ];
    return plan = WeeklyPlan(
      etag: '"e${dismissed.length}"',
      weekStart: plan.weekStart,
      items: items,
    );
  }
}

ExamSchedule sampleSchedule({bool estimated = true}) => ExamSchedule(
  version: 'cal1',
  years: [
    ExamYear(
      examYear: 2027,
      sourceUrl: 'https://www.osym.gov.tr',
      note: 'Tahmini tarihler.',
      events: [
        ExamEvent(
          code: 'tyt',
          name: 'TYT',
          startsAt: DateTime.utc(2027, 6, 19, 7, 15),
          estimated: estimated,
        ),
        ExamEvent(
          code: 'ayt',
          name: 'AYT',
          startsAt: DateTime.utc(2027, 6, 20, 7, 15),
          estimated: estimated,
        ),
      ],
    ),
  ],
);

class FakeExamScheduleRepository implements ExamScheduleRepository {
  FakeExamScheduleRepository([ExamSchedule? schedule])
    : schedule = schedule ?? sampleSchedule();

  ExamSchedule schedule;

  @override
  Future<ExamSchedule?> fetch({String? ifNoneMatch}) async =>
      ifNoneMatch == schedule.version ? null : schedule;
}

class FakeDataExportService implements DataExportService {
  var calls = 0;
  DataExportException? error;

  @override
  Future<void> exportAndShare() async {
    calls++;
    if (error case final e?) {
      throw e;
    }
  }
}
