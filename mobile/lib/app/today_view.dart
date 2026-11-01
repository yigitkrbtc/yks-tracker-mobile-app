import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/design/design.dart';
import '../features/exam_calendar/presentation/exam_calendar_widgets.dart';
import '../features/goals/application/goal_providers.dart';
import '../features/goals/presentation/goal_card.dart';
import '../features/recommendations/application/recommendation_providers.dart';
import '../features/recommendations/presentation/plan_widgets.dart';
import '../features/study_progress/presentation/topic_progress_widgets.dart';

/// The first tab (K14): countdown, today's goal and streak, the plan's first items and curriculum progress.
/// It lives in `app/` because it composes several features' cards.
class TodayView extends ConsumerWidget {
  const TodayView({
    super.key,
    required this.planActions,
    this.onOpenPlan,
    this.onOpenCalendar,
    this.onEditGoal,
    this.onOpenAnalysis,
  });

  final PlanActions planActions;
  final VoidCallback? onOpenPlan;
  final VoidCallback? onOpenCalendar;
  final VoidCallback? onEditGoal;
  final VoidCallback? onOpenAnalysis;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return RefreshIndicator(
      onRefresh: () => ref.read(weeklyPlanProvider.notifier).refresh(),
      child: ListView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        children: [
          CountdownCard(
            now: ref.watch(clockProvider),
            onOpenCalendar: onOpenCalendar,
          ),
          const SizedBox(height: AppSpacing.md),
          GoalCard(onEditGoal: onEditGoal),
          const SizedBox(height: AppSpacing.md),
          PlanSummaryCard(actions: planActions, onOpenPlan: onOpenPlan),
          const SizedBox(height: AppSpacing.md),
          const CurriculumProgressCard(),
          if (onOpenAnalysis case final open?) ...[
            const SizedBox(height: AppSpacing.md),
            NButton(
              label: 'Analizi gör',
              icon: Icons.insights_outlined,
              variant: NButtonVariant.outline,
              expand: true,
              onPressed: open,
            ),
          ],
        ],
      ),
    );
  }
}
