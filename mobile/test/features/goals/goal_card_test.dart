import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/goals/application/goal_providers.dart';
import 'package:nirengi/features/goals/domain/goal_progress.dart';
import 'package:nirengi/features/goals/presentation/goal_card.dart';

import '../../support/design_host.dart';

void main() {
  const goal = StudyGoal(weeklyQuestions: 200, activeDays: 5);
  final summary = (
    progress: GoalProgress(
      goal: goal,
      todayQuestions: 12,
      weekQuestions: 80,
      activeDays: 2,
      weekDays: {for (var d = 1; d <= 7; d++) d: d <= 2},
    ),
    streak: const WeekStreak(weeks: 3, includesThisWeek: false),
  );

  testWidgets('fits a narrow phone with the edit button', (tester) async {
    // 320 dp wide: the smallest common Android width; the day dots used to overflow by ~52 px.
    tester.view.physicalSize = const Size(320, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [goalSummaryProvider.overrideWithValue(summary)],
        child: designHost(
          Scaffold(
            body: SingleChildScrollView(
              padding: const EdgeInsets.all(16),
              child: GoalCard(onEditGoal: () {}),
            ),
          ),
        ),
      ),
    );

    expect(tester.takeException(), isNull);
    expect(find.text('Pt'), findsOneWidget);
    expect(find.text('Pa'), findsOneWidget);
  });
}
