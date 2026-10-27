import 'package:flutter/widgets.dart';
import 'package:shadcn_ui/shadcn_ui.dart';

enum NButtonVariant { primary, secondary, outline, ghost, destructive }

class NButton extends StatelessWidget {
  const NButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.variant = NButtonVariant.primary,
    this.icon,
    this.expand = false,
  });

  final String label;

  /// `null` disables the button.
  final VoidCallback? onPressed;
  final NButtonVariant variant;
  final IconData? icon;

  /// Fills the available width.
  final bool expand;

  @override
  Widget build(BuildContext context) {
    final leading = icon == null ? null : Icon(icon, size: 16);
    final width = expand ? double.infinity : null;
    final child = Text(label);
    return switch (variant) {
      NButtonVariant.primary => ShadButton(
        onPressed: onPressed,
        leading: leading,
        width: width,
        child: child,
      ),
      NButtonVariant.secondary => ShadButton.secondary(
        onPressed: onPressed,
        leading: leading,
        width: width,
        child: child,
      ),
      NButtonVariant.outline => ShadButton.outline(
        onPressed: onPressed,
        leading: leading,
        width: width,
        child: child,
      ),
      NButtonVariant.ghost => ShadButton.ghost(
        onPressed: onPressed,
        leading: leading,
        width: width,
        child: child,
      ),
      NButtonVariant.destructive => ShadButton.destructive(
        onPressed: onPressed,
        leading: leading,
        width: width,
        child: child,
      ),
    };
  }
}
