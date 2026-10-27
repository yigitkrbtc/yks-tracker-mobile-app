import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shadcn_ui/shadcn_ui.dart';

import '../core/config/app_config.dart';
import '../core/design/theme/app_theme.dart';
import 'router.dart';

class NirengiApp extends ConsumerWidget {
  const NirengiApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // ShadApp picks the light/dark shad theme and exposes the matching Material theme via Theme.of.
    return ShadApp.custom(
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      appBuilder: (context) => MaterialApp.router(
        title: AppConfig.appName,
        theme: Theme.of(context),
        routerConfig: ref.watch(routerProvider),
        debugShowCheckedModeBanner: false,
        builder: (context, child) => ShadAppBuilder(child: child),
      ),
    );
  }
}
