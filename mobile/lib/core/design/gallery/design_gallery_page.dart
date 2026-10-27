import 'package:flutter/material.dart';

import '../design.dart';

/// Shows every design-system component; reachable in debug builds from the home app bar.
class DesignGalleryPage extends StatelessWidget {
  const DesignGalleryPage({super.key});

  static const path = '/tasarim';

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Tasarım sistemi')),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        children: [
          const _Section('Düğmeler'),
          Wrap(
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.sm,
            children: [
              for (final v in NButtonVariant.values)
                NButton(label: v.name, variant: v, onPressed: () {}),
              const NButton(label: 'disabled', onPressed: null),
            ],
          ),
          const _Section('Rozetler'),
          Wrap(
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.sm,
            children: [
              for (final t in NBadgeTone.values) NBadge(t.name, tone: t),
            ],
          ),
          const _Section('Kart'),
          NCard(
            title: 'Türev',
            description: 'AYT Matematik · son 30 gün',
            trailing: const NBadge('çok sık', tone: NBadgeTone.primary),
            footer: NButton(
              label: 'Test ekle',
              icon: Icons.add,
              expand: true,
              onPressed: () {},
            ),
            child: const Text('42 soru, %71 doğru'),
          ),
        ],
      ),
    );
  }
}

class _Section extends StatelessWidget {
  const _Section(this.title);

  final String title;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: AppSpacing.xl, bottom: AppSpacing.md),
      child: Text(title, style: Theme.of(context).textTheme.titleMedium),
    );
  }
}
