import 'package:flutter/widgets.dart';
import 'package:shadcn_ui/shadcn_ui.dart';

import '../theme/app_theme.dart';

enum NBadgeTone { neutral, primary, success, warning, danger }

class NBadge extends StatelessWidget {
  const NBadge(this.label, {super.key, this.tone = NBadgeTone.neutral});

  final String label;
  final NBadgeTone tone;

  @override
  Widget build(BuildContext context) {
    final scheme = ShadTheme.of(context).colorScheme;
    final child = Text(label);
    return switch (tone) {
      NBadgeTone.neutral => ShadBadge.secondary(child: child),
      NBadgeTone.primary => ShadBadge(child: child),
      NBadgeTone.danger => ShadBadge.destructive(child: child),
      NBadgeTone.success => ShadBadge(
        backgroundColor: scheme.custom[AppTheme.successKey],
        child: child,
      ),
      NBadgeTone.warning => ShadBadge(
        backgroundColor: scheme.custom[AppTheme.warningKey],
        child: child,
      ),
    };
  }
}
