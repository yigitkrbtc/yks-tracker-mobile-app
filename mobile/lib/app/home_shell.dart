import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/config/app_config.dart';
import '../core/design/gallery/design_gallery_page.dart';
import '../core/sync/sync_providers.dart';
import '../features/analytics/presentation/analysis_format.dart';
import '../features/analytics/presentation/analysis_view.dart';
import '../features/analytics/presentation/topic_analysis_page.dart';
import '../features/auth/application/session_controller.dart';
import '../features/curriculum/presentation/topic_list_view.dart';
import '../features/data_export/application/data_export_providers.dart';
import '../features/data_export/domain/data_export.dart';
import '../features/exam_calendar/presentation/exam_calendar_widgets.dart';
import '../features/goals/application/goal_providers.dart';
import '../features/goals/presentation/goal_settings_page.dart';
import '../features/mock_exams/presentation/mock_exam_form_page.dart';
import '../features/mock_exams/presentation/mock_exam_marks_page.dart';
import '../features/mock_exams/presentation/mock_exam_list_view.dart';
import '../features/profile/presentation/profile_page.dart';
import '../features/topic_tests/presentation/topic_test_form_page.dart';
import '../features/recommendations/presentation/plan_widgets.dart';
import '../features/study_progress/presentation/topic_progress_widgets.dart';
import '../features/topic_tests/presentation/topic_test_list_view.dart';
import 'local_data.dart';
import 'today_view.dart';

/// Plan item taps, shared by the Today card and the plan page.
/// Plan item actions for the Today tab and the plan page. A practice gap opens the mock exams tab inside the
/// shell, and the new mock exam form on the plan page (no tab to switch to there).
PlanActions planActionsFor(
  BuildContext context, {
  VoidCallback? onOpenMockExams,
}) => PlanActions(
  onRecordTest: (topicId) =>
      context.push(TopicTestFormPage.newForTopic(topicId)),
  onTagMockExam: (id) => context.push(MockExamMarksPage.pathFor(id)),
  onOpenMockExams:
      onOpenMockExams ?? () => context.push(MockExamFormPage.newPath),
);

/// Shell with the MVP areas. It lives in `app/` because it composes several features' screens.
class HomeShell extends ConsumerStatefulWidget {
  const HomeShell({super.key});

  @override
  ConsumerState<HomeShell> createState() => _HomeShellState();
}

enum _AccountAction { studyProfile, goals, exportData, logout, deleteAccount }

class _HomeShellState extends ConsumerState<HomeShell> {
  var _index = 0;

  static const _todayTab = 0;
  static const _mockExamsTab = 3;
  static const _analysisTab = 4;

  static const _tabs = [
    (icon: Icons.today_outlined, label: 'Bugün'),
    (icon: Icons.menu_book_outlined, label: 'Konular'),
    (icon: Icons.edit_note_outlined, label: 'Testler'),
    (icon: Icons.assignment_outlined, label: 'Denemeler'),
    (icon: Icons.insights_outlined, label: 'Analiz'),
  ];

  void _onAccountAction(_AccountAction action) {
    switch (action) {
      case _AccountAction.studyProfile:
        context.push(ProfilePage.editPath);
      case _AccountAction.goals:
        context.push(GoalSettingsPage.path);
      case _AccountAction.exportData:
        _exportData();
      case _AccountAction.logout:
        _logout();
      case _AccountAction.deleteAccount:
        context.push('/account/delete');
    }
  }

  @override
  void initState() {
    super.initState();
    // Reminders follow settings and today's progress while someone is signed in.
    ref.listenManual(reminderSyncProvider, (_, _) {}, fireImmediately: true);
  }

  /// The export is built on the server, so writes still on this device are pushed first (K13).
  Future<void> _exportData() async {
    final messenger = ScaffoldMessenger.of(context);
    if (await ref.read(outboxStoreProvider).queuedCount() > 0) {
      await ref.read(syncServiceProvider).sync(force: true);
      if (await ref.read(outboxStoreProvider).queuedCount() > 0) {
        messenger.showSnackBar(
          const SnackBar(
            content: Text(
              'Bazı kayıtlar henüz sunucuya gitmedi; indirilen dosyada olmayacaklar.',
            ),
          ),
        );
      }
    }
    try {
      await ref.read(dataExportServiceProvider).exportAndShare();
    } on DataExportException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  /// Warns when writes have not reached the server yet: signing out removes them from this device.
  Future<void> _logout() async {
    final queued = await ref.read(outboxStoreProvider).queuedCount();
    if (queued > 0) {
      if (!mounted) {
        return;
      }
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Eşitlenmemiş kayıtlar var'),
          content: Text(
            '$queued kayıt henüz sunucuya gitmedi. Çıkış yaparsan bu cihazdan silinir. '
            'İnternete bağlanıp birkaç saniye beklersen kayıtlar gönderilir.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Vazgeç'),
            ),
            TextButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Yine de çık'),
            ),
          ],
        ),
      );
      if (confirmed != true) {
        return;
      }
    }
    await ref.read(localDataProvider).signOutAndWipe();
  }

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(sessionControllerProvider).value;
    final firstName = session is SignedInState
        ? session.profile.firstName
        : null;

    return Scaffold(
      appBar: AppBar(
        title: Text(
          _index == _todayTab && firstName != null
              ? 'Merhaba $firstName'
              : AppConfig.appName,
        ),
        actions: [
          if (kDebugMode)
            IconButton(
              tooltip: 'Tasarım sistemi',
              icon: const Icon(Icons.palette_outlined),
              onPressed: () => context.push(DesignGalleryPage.path),
            ),
          PopupMenuButton<_AccountAction>(
            tooltip: 'Hesap',
            icon: const Icon(Icons.account_circle_outlined),
            onSelected: _onAccountAction,
            itemBuilder: (context) => const [
              PopupMenuItem(
                value: _AccountAction.studyProfile,
                child: Text('Çalışma profilim'),
              ),
              PopupMenuItem(
                value: _AccountAction.goals,
                child: Text('Hedef ve hatırlatmalar'),
              ),
              PopupMenuItem(
                value: _AccountAction.exportData,
                child: Text('Verilerimi indir'),
              ),
              PopupMenuItem(
                value: _AccountAction.logout,
                child: Text('Çıkış yap'),
              ),
              PopupMenuItem(
                value: _AccountAction.deleteAccount,
                child: Text('Hesabımı sil'),
              ),
            ],
          ),
        ],
      ),
      body: switch (_index) {
        _todayTab => TodayView(
          planActions: planActionsFor(
            context,
            onOpenMockExams: () => setState(() => _index = _mockExamsTab),
          ),
          onOpenPlan: () => context.push(WeeklyPlanPage.path),
          onOpenCalendar: () => context.push(ExamCalendarPage.path),
          onEditGoal: () => context.push(GoalSettingsPage.path),
          onOpenAnalysis: () => setState(() => _index = _analysisTab),
        ),
        1 => TopicListView(
          onRecordTest: (topic) =>
              context.push(TopicTestFormPage.newForTopic(topic.id)),
          statusBuilder: (topic) => Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              TopicStatusDot(topicId: topic.id),
              TopicProgressIcon(topicId: topic.id),
            ],
          ),
          markBuilder: (topic) => TopicProgressSelector(topicId: topic.id),
          onOpenAnalysis: (topic) =>
              context.push(TopicAnalysisPage.pathFor(topic.id)),
        ),
        2 => const TopicTestListView(),
        _mockExamsTab => const MockExamListView(),
        _ => const AnalysisView(),
      },
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: [
          for (final t in _tabs)
            NavigationDestination(icon: Icon(t.icon), label: t.label),
        ],
      ),
    );
  }
}
