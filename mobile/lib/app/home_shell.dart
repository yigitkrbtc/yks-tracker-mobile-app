import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/config/app_config.dart';
import '../core/design/design.dart';
import '../core/design/gallery/design_gallery_page.dart';
import '../core/sync/sync_providers.dart';
import '../features/auth/application/session_controller.dart';
import '../features/curriculum/presentation/topic_list_view.dart';
import '../features/mock_exams/presentation/mock_exam_list_view.dart';
import '../features/profile/presentation/profile_page.dart';
import '../features/topic_tests/presentation/topic_test_form_page.dart';
import '../features/topic_tests/presentation/topic_test_list_view.dart';
import 'local_data.dart';

/// Shell with the MVP areas. It lives in `app/` because it composes several features' screens.
class HomeShell extends ConsumerStatefulWidget {
  const HomeShell({super.key});

  @override
  ConsumerState<HomeShell> createState() => _HomeShellState();
}

enum _AccountAction { studyProfile, logout, deleteAccount }

class _HomeShellState extends ConsumerState<HomeShell> {
  var _index = 0;

  static const _tabs = [
    (icon: Icons.menu_book_outlined, label: 'Konular'),
    (icon: Icons.edit_note_outlined, label: 'Testler'),
    (icon: Icons.assignment_outlined, label: 'Denemeler'),
    (icon: Icons.insights_outlined, label: 'Analiz'),
  ];

  void _onAccountAction(_AccountAction action) {
    switch (action) {
      case _AccountAction.studyProfile:
        context.push(ProfilePage.editPath);
      case _AccountAction.logout:
        _logout();
      case _AccountAction.deleteAccount:
        context.push('/account/delete');
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
    final tab = _tabs[_index];
    final session = ref.watch(sessionControllerProvider).value;
    final firstName = session is SignedInState
        ? session.profile.firstName
        : null;

    return Scaffold(
      appBar: AppBar(
        title: Text(
          _index == 0 && firstName != null
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
        0 => TopicListView(
          onRecordTest: (topic) =>
              context.push(TopicTestFormPage.newForTopic(topic.id)),
        ),
        1 => const TopicTestListView(),
        2 => const MockExamListView(),
        _ => Padding(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: NCard(title: tab.label, description: '${tab.label} yakında'),
        ),
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
