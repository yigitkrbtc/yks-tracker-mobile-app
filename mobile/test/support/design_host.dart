import 'package:flutter/material.dart';
import 'package:nirengi/core/design/theme/app_theme.dart';
import 'package:shadcn_ui/shadcn_ui.dart';

/// Hosts a page with the app's themes, as `NirengiApp` does, so `N*` components find their theme.
Widget designHost(Widget home) => ShadApp.custom(
  theme: AppTheme.light(),
  darkTheme: AppTheme.dark(),
  appBuilder: (context) => MaterialApp(
    theme: Theme.of(context),
    builder: (context, child) => ShadAppBuilder(child: child),
    home: home,
  ),
);
