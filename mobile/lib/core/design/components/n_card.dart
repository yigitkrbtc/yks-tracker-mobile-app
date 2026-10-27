import 'package:flutter/widgets.dart';
import 'package:shadcn_ui/shadcn_ui.dart';

import '../tokens/spacing.dart';

class NCard extends StatelessWidget {
  const NCard({
    super.key,
    this.title,
    this.description,
    this.trailing,
    this.child,
    this.footer,
  });

  final String? title;
  final String? description;
  final Widget? trailing;
  final Widget? child;
  final Widget? footer;

  @override
  Widget build(BuildContext context) {
    return ShadCard(
      width: double.infinity,
      padding: const EdgeInsets.all(AppSpacing.lg),
      title: title == null ? null : Text(title!),
      description: description == null ? null : Text(description!),
      trailing: trailing,
      footer: footer,
      child: child == null
          ? null
          : Padding(
              padding: const EdgeInsets.only(top: AppSpacing.md),
              child: child,
            ),
    );
  }
}
