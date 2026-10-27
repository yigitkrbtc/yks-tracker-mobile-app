import 'package:flutter/widgets.dart';
import 'package:shadcn_ui/shadcn_ui.dart';

import '../tokens/colors.dart';
import '../tokens/spacing.dart';

/// Builds the shadcn theme from tokens. `ShadApp` derives the Material theme from it,
/// so Material widgets (Scaffold, NavigationBar) share the same colors.
abstract final class AppTheme {
  static const successKey = 'success';
  static const warningKey = 'warning';

  static ShadThemeData light() => _build(AppColors.light, Brightness.light);

  static ShadThemeData dark() => _build(AppColors.dark, Brightness.dark);

  static ShadThemeData _build(AppPalette p, Brightness brightness) {
    return ShadThemeData(
      brightness: brightness,
      radius: AppRadius.md,
      colorScheme: ShadColorScheme(
        background: p.background,
        foreground: p.foreground,
        card: p.background,
        cardForeground: p.foreground,
        popover: p.background,
        popoverForeground: p.foreground,
        primary: p.primary,
        primaryForeground: p.primaryForeground,
        secondary: p.secondary,
        secondaryForeground: p.secondaryForeground,
        muted: p.muted,
        mutedForeground: p.mutedForeground,
        accent: p.secondary,
        accentForeground: p.secondaryForeground,
        destructive: p.destructive,
        destructiveForeground: p.destructiveForeground,
        border: p.border,
        input: p.border,
        ring: p.primary,
        selection: p.selection,
        custom: {successKey: p.success, warningKey: p.warning},
      ),
    );
  }
}
