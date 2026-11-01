import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/data_export/data/http_data_export_service.dart';
import 'package:nirengi/features/recommendations/domain/weekly_plan.dart';

void main() {
  test('plan JSON reads the API shape and round-trips through the cache', () {
    final plan = WeeklyPlan.fromJson({
      'eTag': '"abc"',
      'weekStart': '2026-10-26',
      'generatedAt': '2026-10-26T06:00:00Z',
      'ruleSetVersion': 'R2026.1',
      'items': [
        {
          'key': 'focus:1',
          'kind': 'Focus',
          'topicId': 't1',
          'sectionCode': null,
          'mockExamId': null,
          'targetQuestions': 30,
          'title': 'Paragraf: odak',
          'text': 'Gerekçe',
          'done': 12,
          'completed': false,
        },
        {
          'key': 'new:1',
          'kind': 'SomethingNew',
          'title': 'Yeni tür',
          'text': '',
          'done': 0,
          'completed': true,
        },
      ],
    });
    expect(plan.items.first.kind, PlanItemKind.focus);
    expect(plan.items.last.kind, PlanItemKind.other);
    expect(plan.completedCount, 1);
    final back = WeeklyPlan.fromJson(plan.toJson());
    expect(back.etag, '"abc"');
    expect(back.items.first.done, 12);
  });

  test('export file name comes from the server, made safe', () {
    expect(
      HttpDataExportService.fileName(
        'attachment; filename=nirengi-verilerim-2026-11-01.zip; filename*=UTF-8\'\'x',
      ),
      'nirengi-verilerim-2026-11-01.zip',
    );
    expect(
      HttpDataExportService.fileName('attachment; filename="../../x.zip"'),
      'x.zip',
    );
    expect(
      HttpDataExportService.fileName(null),
      HttpDataExportService.fallbackName,
    );
    expect(
      HttpDataExportService.fileName('attachment; filename=a.exe'),
      HttpDataExportService.fallbackName,
    );
  });
}
