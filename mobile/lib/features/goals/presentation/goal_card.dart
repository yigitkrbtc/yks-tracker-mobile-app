import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design/design.dart';
import '../application/goal_providers.dart';
import '../domain/goal_progress.dart';

/// Today's ring, this week's active days and the week streak.
class GoalCard extends ConsumerWidget {
  const GoalCard({super.key, this.onEditGoal});

  final VoidCallback? onEditGoal;

  static const _dayLetters = ['Pt', 'Sa', 'Ça', 'Pe', 'Cu', 'Ct', 'Pa'];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final summary = ref.watch(goalSummaryProvider);
    if (summary == null) {
      return const SizedBox.shrink();
    }
    final (:progress, :streak) = summary;
    final goal = progress.goal;
    final theme = Theme.of(context);
    return NCard(
      title: 'Hedefin',
      description:
          'Günde ${goal.dailyQuestions} soru · haftada ${goal.activeDays} aktif gün',
      trailing: onEditGoal == null
          ? null
          : IconButton(
              tooltip: 'Hedefi düzenle',
              icon: const Icon(Icons.tune),
              onPressed: onEditGoal,
            ),
      child: Row(
        children: [
          SizedBox.square(
            dimension: 72,
            child: Stack(
              fit: StackFit.expand,
              children: [
                CircularProgressIndicator(
                  value: progress.todayShare,
                  strokeWidth: 8,
                  backgroundColor: theme.colorScheme.surfaceContainerHighest,
                ),
                Center(
                  child: Text(
                    '${progress.todayQuestions}',
                    key: const Key('goal-today-questions'),
                    style: theme.textTheme.titleMedium,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: AppSpacing.lg),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  progress.todayMet
                      ? 'Bugünün hedefi tamam.'
                      : 'Bugün ${goal.dailyQuestions - progress.todayQuestions} soru kaldı.',
                  style: theme.textTheme.bodyMedium,
                ),
                const SizedBox(height: AppSpacing.sm),
                // Wrap, not Row: seven fixed-size dots do not fit next to the ring on narrow phones.
                Wrap(
                  spacing: AppSpacing.xs,
                  runSpacing: AppSpacing.xs,
                  children: [
                    for (var d = 1; d <= 7; d++)
                      _DayDot(
                        label: _dayLetters[d - 1],
                        active: progress.weekDays[d] ?? false,
                      ),
                  ],
                ),
                const SizedBox(height: AppSpacing.sm),
                Text(
                  _weekLine(progress, streak),
                  key: const Key('goal-week-line'),
                  style: theme.textTheme.bodySmall,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static String _weekLine(GoalProgress p, WeekStreak s) {
    final week =
        'Bu hafta ${p.activeDays}/${p.goal.activeDays} gün, ${p.weekQuestions}/${p.goal.weeklyQuestions} soru';
    return s.weeks == 0 ? week : '$week · ${s.weeks} haftalık seri';
  }
}

class _DayDot extends StatelessWidget {
  const _DayDot({required this.label, required this.active});

  final String label;
  final bool active;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      width: 26,
      height: 26,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: active ? scheme.primary : scheme.surfaceContainerHighest,
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 10,
          color: active ? scheme.onPrimary : scheme.onSurfaceVariant,
        ),
      ),
    );
  }
}
