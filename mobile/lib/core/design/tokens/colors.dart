import 'package:flutter/widgets.dart';

/// Brand and semantic colors. The single source for every theme in the app.
abstract final class AppColors {
  static const brand = Color(0xFF2F5D8A);

  static const light = AppPalette(
    background: Color(0xFFFFFFFF),
    foreground: Color(0xFF0F1B2A),
    muted: Color(0xFFF1F4F8),
    mutedForeground: Color(0xFF5B6B7F),
    border: Color(0xFFDDE3EA),
    primary: brand,
    primaryForeground: Color(0xFFF7FAFD),
    secondary: Color(0xFFE8EFF6),
    secondaryForeground: Color(0xFF1C3A57),
    destructive: Color(0xFFD64545),
    destructiveForeground: Color(0xFFFFFFFF),
    success: Color(0xFF2E8B57),
    warning: Color(0xFFC98A12),
    selection: Color(0xFFB9D3EC),
  );

  static const dark = AppPalette(
    background: Color(0xFF0B1420),
    foreground: Color(0xFFE9EEF4),
    muted: Color(0xFF16222F),
    mutedForeground: Color(0xFF93A3B5),
    border: Color(0xFF243243),
    primary: Color(0xFF7FA9D3),
    primaryForeground: Color(0xFF0B1420),
    secondary: Color(0xFF1B2A3A),
    secondaryForeground: Color(0xFFD5E2EF),
    destructive: Color(0xFFE26464),
    destructiveForeground: Color(0xFF0B1420),
    success: Color(0xFF5BBF86),
    warning: Color(0xFFE2AD45),
    selection: Color(0xFF2D4A68),
  );
}

@immutable
class AppPalette {
  const AppPalette({
    required this.background,
    required this.foreground,
    required this.muted,
    required this.mutedForeground,
    required this.border,
    required this.primary,
    required this.primaryForeground,
    required this.secondary,
    required this.secondaryForeground,
    required this.destructive,
    required this.destructiveForeground,
    required this.success,
    required this.warning,
    required this.selection,
  });

  final Color background;
  final Color foreground;
  final Color muted;
  final Color mutedForeground;
  final Color border;
  final Color primary;
  final Color primaryForeground;
  final Color secondary;
  final Color secondaryForeground;
  final Color destructive;
  final Color destructiveForeground;
  final Color success;
  final Color warning;
  final Color selection;
}
