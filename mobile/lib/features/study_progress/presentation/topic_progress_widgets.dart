import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design/design.dart';
import '../application/topic_progress_providers.dart';
import '../domain/topic_progress.dart';

/// Small icon for list rows; empty while the topic is unmarked.
class TopicProgressIcon extends ConsumerWidget {
  const TopicProgressIcon({super.key, required this.topicId});

  final String topicId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final scheme = Theme.of(context).colorScheme;
    return switch (ref.watch(topicMarkProvider(topicId))) {
      ProgressStatus.done => Icon(
        Icons.check_circle,
        size: 18,
        color: scheme.primary,
        semanticLabel: ProgressStatus.done.label,
      ),
      ProgressStatus.inProgress => Icon(
        Icons.timelapse,
        size: 18,
        color: scheme.primary,
        semanticLabel: ProgressStatus.inProgress.label,
      ),
      _ => const SizedBox(width: 18),
    };
  }
}

/// Three-way mark for the topic sheet; saved on the device at once and synced later.
class TopicProgressSelector extends ConsumerWidget {
  const TopicProgressSelector({super.key, required this.topicId});

  final String topicId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final current = ref.watch(topicMarkProvider(topicId));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Bu konuda neredesin?',
          style: Theme.of(context).textTheme.titleSmall,
        ),
        const SizedBox(height: AppSpacing.sm),
        SegmentedButton<ProgressStatus>(
          key: const Key('topic-progress-selector'),
          emptySelectionAllowed: true,
          showSelectedIcon: false,
          segments: [
            for (final s in ProgressStatus.values)
              ButtonSegment(value: s, label: Text(_short(s))),
          ],
          selected: {?current},
          onSelectionChanged: (selection) {
            if (selection.isNotEmpty) {
              ref
                  .read(topicProgressRepositoryProvider)
                  .mark(topicId, selection.single);
            }
          },
        ),
      ],
    );
  }

  static String _short(ProgressStatus s) => switch (s) {
    ProgressStatus.notStarted => 'Başlamadım',
    ProgressStatus.inProgress => 'Çalışıyorum',
    ProgressStatus.done => 'Bitirdim',
  };
}

/// "12 / 140 konu bitti" with the ÖSYM-weighted share below (K8).
class CurriculumProgressCard extends ConsumerWidget {
  const CurriculumProgressCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final progress = ref.watch(curriculumProgressProvider);
    if (progress == null || progress.total == 0) {
      return const SizedBox.shrink();
    }
    final textTheme = Theme.of(context).textTheme;
    return NCard(
      title: 'Müfredat',
      description:
          '${progress.done} / ${progress.total} konu bitti'
          '${progress.inProgress > 0 ? ' · ${progress.inProgress} konu sürüyor' : ''}',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          LinearProgressIndicator(value: progress.share, minHeight: 8),
          const SizedBox(height: AppSpacing.sm),
          Text(
            'Sınavdaki soruların %${(progress.weightedShare * 100).round()} kadarı bitirdiğin konulardan.',
            style: textTheme.bodySmall,
          ),
        ],
      ),
    );
  }
}
