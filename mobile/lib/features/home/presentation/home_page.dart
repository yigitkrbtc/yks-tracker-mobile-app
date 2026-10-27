import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../core/config/app_config.dart';
import '../../../core/design/design.dart';
import '../../../core/design/gallery/design_gallery_page.dart';

/// Shell with the three MVP areas. Each tab is filled by its own feature in later sprints.
class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  var _index = 0;

  static const _tabs = [
    (icon: Icons.menu_book_outlined, label: 'Konular'),
    (icon: Icons.assignment_outlined, label: 'Denemeler'),
    (icon: Icons.insights_outlined, label: 'Analiz'),
  ];

  @override
  Widget build(BuildContext context) {
    final tab = _tabs[_index];
    return Scaffold(
      appBar: AppBar(
        title: const Text(AppConfig.appName),
        actions: [
          if (kDebugMode)
            IconButton(
              tooltip: 'Tasarım sistemi',
              icon: const Icon(Icons.palette_outlined),
              onPressed: () => context.push(DesignGalleryPage.path),
            ),
        ],
      ),
      body: Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: NCard(title: tab.label, description: '${tab.label} yakında'),
      ),
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
