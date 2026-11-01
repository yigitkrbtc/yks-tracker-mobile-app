import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design/design.dart';
import '../application/recommendation_providers.dart';
import '../domain/recommendation_repository.dart';
import '../domain/weekly_plan.dart';

/// Where tapping an item goes; wired by the app shell (features do not open each other's screens).
class PlanActions {
  const PlanActions({
    this.onRecordTest,
    this.onTagMockExam,
    this.onOpenMockExams,
  });

  final void Function(String topicId)? onRecordTest;
  final void Function(String mockExamId)? onTagMockExam;
  final VoidCallback? onOpenMockExams;
}

/// The first three items for the Today screen.
class PlanSummaryCard extends ConsumerWidget {
  const PlanSummaryCard({super.key, required this.actions, this.onOpenPlan});

  static const shown = 3;

  final PlanActions actions;
  final VoidCallback? onOpenPlan;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final plan = ref.watch(weeklyPlanProvider);
    final value = plan.value;
    return NCard(
      title: 'Bu haftanın planı',
      description: value == null
          ? null
          : value.items.isEmpty
          ? 'Şimdilik öneri yok; kayıt ekledikçe plan oluşur.'
          : '${value.completedCount} / ${value.items.length} tamam',
      footer: value == null || value.items.length <= shown || onOpenPlan == null
          ? null
          : Align(
              alignment: Alignment.centerLeft,
              child: TextButton(
                onPressed: onOpenPlan,
                child: Text('Tüm plan (${value.items.length})'),
              ),
            ),
      child: switch (plan) {
        AsyncData(value: final p?) when p.items.isNotEmpty => Column(
          children: [
            for (final item in p.items.take(shown))
              PlanItemTile(item: item, actions: actions, compact: true),
          ],
        ),
        AsyncError(:final error) => Text(
          error is RecommendationException
              ? error.message
              : 'Plan yüklenemedi.',
        ),
        AsyncLoading() => const LinearProgressIndicator(),
        _ => null,
      },
    );
  }
}

/// The whole week: every item with its reasoning and a dismiss menu.
class WeeklyPlanPage extends ConsumerWidget {
  const WeeklyPlanPage({super.key, required this.actions});

  static const path = '/plan';

  final PlanActions actions;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final plan = ref.watch(weeklyPlanProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Bu haftanın planı')),
      body: RefreshIndicator(
        onRefresh: () => ref.read(weeklyPlanProvider.notifier).refresh(),
        child: switch (plan) {
          AsyncData(value: final p?) => ListView(
            padding: const EdgeInsets.all(AppSpacing.lg),
            children: [
              Text(
                'Plan haftanın başında analizine göre kurulur ve hafta boyunca sabit kalır; '
                'ilerleme kaydettikçe güncellenir.',
                style: Theme.of(context).textTheme.bodySmall,
              ),
              const SizedBox(height: AppSpacing.md),
              if (p.items.isEmpty)
                const Text('Şimdilik öneri yok; kayıt ekledikçe plan oluşur.'),
              for (final item in p.items)
                Padding(
                  padding: const EdgeInsets.only(bottom: AppSpacing.md),
                  child: NCard(
                    child: PlanItemTile(
                      item: item,
                      actions: actions,
                      compact: false,
                    ),
                  ),
                ),
            ],
          ),
          AsyncError(:final error) => ListView(
            children: [
              Padding(
                padding: const EdgeInsets.all(AppSpacing.xl),
                child: Text(
                  error is RecommendationException
                      ? error.message
                      : 'Plan yüklenemedi.',
                ),
              ),
            ],
          ),
          _ => const Center(child: CircularProgressIndicator()),
        },
      ),
    );
  }
}

class PlanItemTile extends ConsumerWidget {
  const PlanItemTile({
    super.key,
    required this.item,
    required this.actions,
    required this.compact,
  });

  final PlanItem item;
  final PlanActions actions;
  final bool compact;

  VoidCallback? _onTap() {
    final topic = item.topicId;
    final exam = item.mockExamId;
    final tag = actions.onTagMockExam;
    final record = actions.onRecordTest;
    if (item.kind == PlanItemKind.tagMockExam && exam != null && tag != null) {
      return () => tag(exam);
    }
    if (item.kind == PlanItemKind.practiceGap ||
        item.kind == PlanItemKind.tagMockExam) {
      return actions.onOpenMockExams;
    }
    if (topic != null && record != null) {
      return () => record(topic);
    }
    return null;
  }

  Future<void> _dismiss(
    BuildContext context,
    WidgetRef ref,
    DismissReason reason,
  ) async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(weeklyPlanProvider.notifier).dismiss(item, reason);
      messenger.showSnackBar(
        const SnackBar(
          content: Text('Öneri kaldırıldı; yerine sıradaki geldi.'),
        ),
      );
    } on RecommendationException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final textTheme = Theme.of(context).textTheme;
    final target = item.targetQuestions;
    final progress = target == null
        ? null
        : '${item.done > target ? target : item.done} / $target soru';
    // Cards paint their own background; a transparent Material keeps the tile's ink visible on it.
    return Material(
      type: MaterialType.transparency,
      child: ListTile(
        key: Key('plan-item-${item.key}'),
        contentPadding: EdgeInsets.zero,
        leading: Icon(
          item.completed ? Icons.check_circle : _icon(item.kind),
          color: item.completed ? Theme.of(context).colorScheme.primary : null,
        ),
        title: Text(item.title),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (!compact) ...[
              const SizedBox(height: AppSpacing.xs),
              Text(item.text, style: textTheme.bodySmall),
            ],
            if (progress != null) ...[
              const SizedBox(height: AppSpacing.xs),
              Text(progress, style: textTheme.labelSmall),
            ],
          ],
        ),
        onTap: _onTap(),
        trailing: compact
            ? null
            : PopupMenuButton<DismissReason>(
                tooltip: 'Öneriyi kaldır',
                icon: const Icon(Icons.more_vert),
                onSelected: (reason) => _dismiss(context, ref, reason),
                itemBuilder: (context) => [
                  for (final r in DismissReason.values)
                    if (r != DismissReason.alreadyKnow || item.topicId != null)
                      PopupMenuItem(value: r, child: Text(r.label)),
                ],
              ),
      ),
    );
  }

  static IconData _icon(PlanItemKind kind) => switch (kind) {
    PlanItemKind.tagMockExam => Icons.label_outline,
    PlanItemKind.focus => Icons.center_focus_strong_outlined,
    PlanItemKind.decline => Icons.trending_down,
    PlanItemKind.collectData => Icons.add_chart_outlined,
    PlanItemKind.review => Icons.replay,
    PlanItemKind.practiceGap => Icons.compare_arrows,
    PlanItemKind.firstRecord => Icons.flag_outlined,
    PlanItemKind.other => Icons.lightbulb_outline,
  };
}
