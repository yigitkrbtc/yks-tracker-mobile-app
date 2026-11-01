import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/notifications/local_reminder_scheduler.dart';
import '../../../core/notifications/notification_providers.dart';
import '../../profile/application/profile_providers.dart';
import '../domain/goal_progress.dart';
import '../domain/goal_repositories.dart';
import '../domain/reminders.dart';
import '../domain/study_day.dart';

/// Bound to the Drift implementation in `app/composition.dart`.
final activityRepositoryProvider = Provider<ActivityRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

/// Bound to the device cache in `app/composition.dart`; defaults to memory.
final reminderSettingsStoreProvider = Provider<ReminderSettingsStore>(
  (ref) => MemoryReminderSettingsStore(),
);

class MemoryReminderSettingsStore implements ReminderSettingsStore {
  ReminderSettings _settings = const ReminderSettings();

  @override
  Future<ReminderSettings> read() async => _settings;

  @override
  Future<void> write(ReminderSettings settings) async => _settings = settings;
}

/// The clock; tests pin it.
final clockProvider = Provider<DateTime Function()>((ref) => DateTime.now);

/// Weeks of history the streak looks back over.
const streakLookbackWeeks = 52;

final _activityProvider = StreamProvider<List<DayActivity>>((ref) {
  final today = turkeyDay(ref.watch(clockProvider)());
  final from = weekStart(today)
      .subtract(const Duration(days: 7 * streakLookbackWeeks));
  return ref.watch(activityRepositoryProvider).watchSince(from);
});

final studyGoalProvider = Provider<StudyGoal>((ref) {
  final profile = ref.watch(studyProfileProvider).value;
  return StudyGoal.of(
    weeklyQuestions: profile?.weeklyQuestionGoal,
    activeDays: profile?.weeklyActiveDaysGoal,
  );
});

typedef GoalSummary = ({GoalProgress progress, WeekStreak streak});

/// Today, this week and the streak; null until the records are read.
final goalSummaryProvider = Provider<GoalSummary?>((ref) {
  final activity = ref.watch(_activityProvider).value;
  if (activity == null) {
    return null;
  }
  final goal = ref.watch(studyGoalProvider);
  final today = turkeyDay(ref.watch(clockProvider)());
  return (
    progress: GoalProgress.of(activity, goal, today),
    streak: WeekStreak.of(activity, goal, today),
  );
});

final reminderSettingsProvider =
    AsyncNotifierProvider<ReminderSettingsController, ReminderSettings>(
      ReminderSettingsController.new,
    );

/// Reminder choices; saving one reschedules at once.
class ReminderSettingsController extends AsyncNotifier<ReminderSettings> {
  @override
  Future<ReminderSettings> build() =>
      ref.read(reminderSettingsStoreProvider).read();

  /// Turning a reminder on asks for the notification permission; false when the student refused it (the
  /// setting is then left off).
  Future<bool> save(ReminderSettings settings) async {
    final before = state.value ?? const ReminderSettings();
    if (settings.any && !before.any) {
      final allowed = await ref
          .read(localReminderSchedulerProvider)
          .requestPermission();
      if (!allowed) {
        return false;
      }
    }
    await ref.read(reminderSettingsStoreProvider).write(settings);
    state = AsyncData(settings);
    return true;
  }
}

/// Keeps scheduled reminders in step with settings and today's progress: today's daily reminder is dropped
/// once its goal is met. Watched by the app root while signed in.
final reminderSyncProvider = Provider<void>((ref) {
  final settings = ref.watch(reminderSettingsProvider).value;
  final summary = ref.watch(goalSummaryProvider);
  if (settings == null || summary == null) {
    return;
  }
  final planned = ReminderPlanner.plan(
    settings: settings,
    now: ref.read(clockProvider)(),
    todayMet: summary.progress.todayMet,
    dailyQuestions: summary.progress.goal.dailyQuestions,
  );
  unawaited(
    ref.read(localReminderSchedulerProvider).replaceAll([
      for (final r in planned)
        LocalReminder(id: r.id, at: r.at, title: r.title, body: r.body),
    ]),
  );
});
