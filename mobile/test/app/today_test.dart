import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/app/app.dart';
import 'package:nirengi/core/notifications/local_reminder_scheduler.dart';
import 'package:nirengi/core/notifications/notification_providers.dart';
import 'package:nirengi/core/sync/sync_providers.dart';
import 'package:nirengi/features/data_export/domain/data_export.dart';
import 'package:nirengi/features/mock_exams/presentation/mock_exam_form_page.dart';
import 'package:nirengi/features/recommendations/domain/weekly_plan.dart';

import '../support/app_fakes.dart';
import '../support/auth_fakes.dart';
import '../support/shell.dart';
import '../support/student_tools_fakes.dart';

class FakeReminderScheduler implements LocalReminderScheduler {
  var allow = true;
  var permissionAsked = 0;
  List<LocalReminder> scheduled = const [];

  @override
  Future<bool> requestPermission() async {
    permissionAsked++;
    return allow;
  }

  @override
  Future<void> replaceAll(List<LocalReminder> reminders) async =>
      scheduled = reminders;
}

void main() {
  // Wednesday 4 November 2026, 18:00 in Türkiye.
  final now = DateTime.utc(2026, 11, 4, 15);
  late FakeRecommendationRepository recommendations;
  late FakeProfileRepository profiles;
  late FakeDataExportService export;
  late FakeReminderScheduler reminders;

  setUp(() {
    recommendations = FakeRecommendationRepository();
    profiles = FakeProfileRepository(sayProfile);
    export = FakeDataExportService();
    reminders = FakeReminderScheduler();
  });

  Future<ProviderContainer> pumpApp(WidgetTester tester) async {
    // A phone-sized screen, so the Today cards fit without scrolling.
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          ...fakeAppOverrides(
            store: MemorySessionStore(refreshToken: 'refresh-old'),
            profiles: profiles,
            recommendations: recommendations,
            dataExport: export,
            now: () => now,
          ),
          localReminderSchedulerProvider.overrideWithValue(reminders),
        ],
        child: const NirengiApp(),
      ),
    );
    await tester.pumpAndSettle();
    return ProviderScope.containerOf(tester.element(find.byType(NirengiApp)));
  }

  Future<void> openAccountMenu(WidgetTester tester, String item) async {
    await tester.tap(find.byIcon(Icons.account_circle_outlined));
    await tester.pumpAndSettle();
    await tester.tap(find.text(item));
    await tester.pumpAndSettle();
  }

  testWidgets(
    'a new student sees the countdown, an empty goal and no plan yet',
    (tester) async {
      await pumpApp(tester);

      expect(find.text('Bugün'), findsOneWidget);
      expect(find.text('227 gün kaldı'), findsOneWidget);
      expect(find.text('Tahmini'), findsOneWidget);
      expect(find.text('Bugün 40 soru kaldı.'), findsOneWidget);
      expect(find.text('Bu hafta 0/5 gün, 0/200 soru'), findsOneWidget);
      expect(
        find.text('Şimdilik öneri yok; kayıt ekledikçe plan oluşur.'),
        findsOneWidget,
      );
      await disposeShell(tester);
    },
  );

  testWidgets(
    'the plan shows three items; the page dismisses one and the next takes its place',
    (tester) async {
      recommendations.plan = WeeklyPlan(
        etag: '"p1"',
        weekStart: '2026-11-02',
        items: [
          planItem('focus:a', topicId: 'TYT.TUR-1', done: 12),
          planItem('focus:b', topicId: 'TYT.TMAT-1'),
          planItem(
            'collect:c',
            kind: PlanItemKind.collectData,
            topicId: 'TYT.FIZ-1',
            completed: true,
          ),
          planItem('review:d', kind: PlanItemKind.review, topicId: 'TYT.KIM-1'),
        ],
      );
      recommendations.replacements.add(
        planItem('focus:e', topicId: 'TYT.BIY-1'),
      );
      await pumpApp(tester);

      expect(find.text('1 / 4 tamam'), findsOneWidget);
      expect(find.text('Başlık focus:a'), findsOneWidget);
      expect(find.text('12 / 30 soru'), findsOneWidget);
      expect(find.text('Başlık review:d'), findsNothing);

      await tester.tap(find.text('Tüm plan (4)'));
      await tester.pumpAndSettle();
      expect(find.text('Gerekçe focus:a'), findsOneWidget);

      await tester.tap(
        find.descendant(
          of: find.byKey(const Key('plan-item-focus:b')),
          matching: find.byIcon(Icons.more_vert),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Bu konuyu biliyorum'));
      await tester.pumpAndSettle();

      expect(recommendations.dismissed.single.$1, 'focus:b');
      expect(find.text('Başlık focus:b'), findsNothing);
      expect(find.text('Başlık focus:e'), findsOneWidget);
      expect(
        find.text('Öneri kaldırıldı; yerine sıradaki geldi.'),
        findsOneWidget,
      );
      await disposeShell(tester);
    },
  );

  testWidgets('a practice gap on the plan page opens the new mock exam form', (
    tester,
  ) async {
    recommendations.plan = WeeklyPlan(
      etag: '"p1"',
      weekStart: '2026-11-02',
      items: [
        planItem('focus:a', topicId: 'TYT.TUR-1'),
        planItem('focus:b', topicId: 'TYT.TMAT-1'),
        planItem('focus:c', topicId: 'TYT.FIZ-1'),
        planItem('gap:TYT-TUR', kind: PlanItemKind.practiceGap, target: null),
      ],
    );
    await pumpApp(tester);

    await tester.tap(find.text('Tüm plan (4)'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Başlık gap:TYT-TUR'));
    await tester.pumpAndSettle();

    expect(find.byType(MockExamFormPage), findsOneWidget);
    await disposeShell(tester);
  });

  testWidgets('marking a topic shows in the list and queues a sync', (
    tester,
  ) async {
    final container = await pumpApp(tester);
    container.read(syncSchedulerProvider).stop();
    await openTopics(tester);
    await tester.tap(find.text('Türkçe'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Türkçe konu 1'));
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.text('Bitirdim'));
    await tester.tap(find.text('Bitirdim'));
    await tester.pumpAndSettle();
    Navigator.of(tester.element(find.text('Bitirdim'))).pop();
    await tester.pumpAndSettle();

    expect(find.byIcon(Icons.check_circle), findsOneWidget);
    expect(
      await tester.runAsync(container.read(outboxStoreProvider).queuedCount),
      1,
    );
    await disposeShell(tester);
  });

  testWidgets('goal settings save the weekly goal and schedule reminders', (
    tester,
  ) async {
    await pumpApp(tester);
    await openAccountMenu(tester, 'Hedef ve hatırlatmalar');

    await tester.enterText(find.byKey(const Key('goal-weekly-field')), '120');
    await tester.pumpAndSettle();
    expect(find.textContaining('çalıştığın gün 24 soru'), findsOneWidget);
    await tester.tap(find.byKey(const Key('reminder-daily-switch')));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Kaydet'));
    await tester.tap(find.text('Kaydet'));
    await tester.pumpAndSettle();

    expect(profiles.saved.single.weeklyQuestionGoal, 120);
    expect(profiles.saved.single.weeklyActiveDaysGoal, 5);
    expect(reminders.permissionAsked, 1);
    expect(reminders.scheduled, hasLength(7));
    expect(reminders.scheduled.first.body, contains('24 soru'));
    expect(find.text('Hedef ve hatırlatmalar'), findsNothing);
    expect(find.text('Günde 24 soru · haftada 5 aktif gün'), findsOneWidget);
    await disposeShell(tester);
  });

  testWidgets('refused notification permission leaves reminders off', (
    tester,
  ) async {
    reminders.allow = false;
    await pumpApp(tester);
    await openAccountMenu(tester, 'Hedef ve hatırlatmalar');

    await tester.tap(find.byKey(const Key('reminder-weekly-switch')));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Kaydet'));
    await tester.tap(find.text('Kaydet'));
    await tester.pumpAndSettle();

    expect(
      find.textContaining('Bildirim izni verilmediği için'),
      findsOneWidget,
    );
    expect(reminders.scheduled, isEmpty);
    await disposeShell(tester);
  });

  testWidgets('"Verilerimi indir" downloads and shares the export', (
    tester,
  ) async {
    await pumpApp(tester);
    await openAccountMenu(tester, 'Verilerimi indir');
    expect(export.calls, 1);

    export.error = const DataExportException(
      'Bir saatte en çok 3 kez indirebilirsin.',
    );
    await openAccountMenu(tester, 'Verilerimi indir');
    expect(
      find.text('Bir saatte en çok 3 kez indirebilirsin.'),
      findsOneWidget,
    );
    await disposeShell(tester);
  });
}
