import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/design/theme/app_theme.dart';
import 'package:nirengi/features/exam_calendar/application/exam_schedule_providers.dart';
import 'package:nirengi/features/exam_calendar/domain/exam_schedule.dart';
import 'package:nirengi/features/exam_calendar/presentation/exam_calendar_widgets.dart';
import 'package:shadcn_ui/shadcn_ui.dart';

void main() {
  // TYT 2027 at 10:15 in Türkiye.
  final tyt = DateTime.utc(2027, 6, 19, 7, 15);
  final year = ExamYear(
    examYear: 2027,
    sourceUrl: 'https://www.osym.gov.tr',
    events: [
      ExamEvent(code: 'tyt', name: 'TYT', startsAt: tyt, estimated: false),
    ],
  );

  Future<void> pump(WidgetTester tester, DateTime now) => tester.pumpWidget(
    ProviderScope(
      overrides: [targetExamYearProvider.overrideWithValue(year)],
      child: ShadApp.custom(
        theme: AppTheme.light(),
        appBuilder: (context) => MaterialApp(
          theme: Theme.of(context),
          builder: (context, c) => ShadAppBuilder(child: c),
          home: Scaffold(body: CountdownCard(now: () => now)),
        ),
      ),
    ),
  );

  testWidgets('the day before shows one day left', (tester) async {
    await pump(tester, DateTime.utc(2027, 6, 18, 12));
    expect(find.text('1 gün kaldı'), findsOneWidget);
  });

  testWidgets('exam day is not shown as passed', (tester) async {
    await pump(tester, DateTime.utc(2027, 6, 19, 6));
    expect(find.text('Sınav bugün, başarılar!'), findsOneWidget);
    expect(find.textContaining('geçti'), findsNothing);
  });

  testWidgets('the day after says the exam passed', (tester) async {
    await pump(tester, DateTime.utc(2027, 6, 20, 6));
    expect(find.textContaining('Sınav günü geçti'), findsOneWidget);
    expect(find.byKey(const Key('countdown-days')), findsNothing);
  });
}
