import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design/design.dart';
import '../../profile/application/profile_providers.dart';
import '../../profile/domain/profile_repository.dart';
import '../../profile/domain/study_profile.dart';
import '../application/goal_providers.dart';
import '../domain/goal_progress.dart';
import '../domain/reminders.dart';

/// Weekly goal (kept in the study profile, synced) and reminders (this device only).
class GoalSettingsPage extends ConsumerStatefulWidget {
  const GoalSettingsPage({super.key});

  static const path = '/goals';

  @override
  ConsumerState<GoalSettingsPage> createState() => _GoalSettingsPageState();
}

class _GoalSettingsPageState extends ConsumerState<GoalSettingsPage> {
  final _weekly = TextEditingController();
  var _activeDays = StudyGoal.defaultActiveDays;
  ReminderSettings? _reminders;
  var _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    final goal = ref.read(studyGoalProvider);
    _weekly.text = goal.weeklyQuestions.toString();
    _activeDays = goal.activeDays;
    _reminders = ref.read(reminderSettingsProvider).value;
  }

  @override
  void dispose() {
    _weekly.dispose();
    super.dispose();
  }

  int? get _weeklyValue {
    final v = int.tryParse(_weekly.text.trim());
    return v != null && v >= 1 && v <= StudyProfile.maxWeeklyQuestionGoal
        ? v
        : null;
  }

  Future<void> _save() async {
    final weekly = _weeklyValue;
    final profile = ref.read(studyProfileProvider).value;
    if (weekly == null || profile == null) {
      return;
    }
    final router = GoRouter.of(context);
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ref
          .read(studyProfileProvider.notifier)
          .save(
            profile.withGoals(
              weeklyQuestionGoal: weekly,
              weeklyActiveDaysGoal: _activeDays,
            ),
          );
      final reminders = _reminders;
      if (reminders != null &&
          !await ref.read(reminderSettingsProvider.notifier).save(reminders)) {
        if (!mounted) {
          return;
        }
        setState(() {
          _saving = false;
          _error =
              'Bildirim izni verilmediği için hatırlatmalar kapalı kaldı. '
              'İzni telefon ayarlarından açabilirsin.';
          _reminders = ref.read(reminderSettingsProvider).value;
        });
        return;
      }
      router.pop();
    } on ProfileException catch (e) {
      if (!mounted) {
        return;
      }
      setState(() {
        _saving = false;
        _error = e.message;
      });
    }
  }

  Future<void> _pickTime(ReminderSettings r) async {
    final picked = await showTimePicker(
      context: context,
      initialTime: TimeOfDay(hour: r.hour, minute: r.minute),
    );
    if (picked != null) {
      setState(
        () => _reminders = r.copyWith(hour: picked.hour, minute: picked.minute),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    _reminders ??= ref.watch(reminderSettingsProvider).value;
    final reminders = _reminders ?? const ReminderSettings();
    final weekly = _weeklyValue;
    final daily = weekly == null ? null : (weekly / _activeDays).ceil();
    final textTheme = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Hedef ve hatırlatmalar')),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.xl),
        children: [
          TextField(
            key: const Key('goal-weekly-field'),
            controller: _weekly,
            keyboardType: TextInputType.number,
            decoration: InputDecoration(
              labelText: 'Haftalık soru hedefi',
              errorText: weekly == null
                  ? '1 ile ${StudyProfile.maxWeeklyQuestionGoal} arasında olmalı.'
                  : null,
            ),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: AppSpacing.lg),
          Text('Haftada kaç gün çalışacaksın?', style: textTheme.titleSmall),
          Slider(
            key: const Key('goal-days-slider'),
            value: _activeDays.toDouble(),
            min: 1,
            max: 7,
            divisions: 6,
            label: '$_activeDays gün',
            onChanged: (v) => setState(() => _activeDays = v.round()),
          ),
          Text(
            daily == null
                ? 'Haftada $_activeDays gün.'
                : 'Haftada $_activeDays gün, çalıştığın gün $daily soru. '
                      'Hedefi tutan her hafta seriyi bir artırır.',
            key: const Key('goal-daily-hint'),
            style: textTheme.bodySmall,
          ),
          const Divider(height: AppSpacing.xl * 2),
          Text('Hatırlatmalar', style: textTheme.titleSmall),
          Text(
            'Yalnız bu telefonda; bildirim sunucudan gelmez.',
            style: textTheme.bodySmall,
          ),
          SwitchListTile(
            key: const Key('reminder-daily-switch'),
            contentPadding: EdgeInsets.zero,
            title: const Text('Günlük hatırlatma'),
            subtitle: Text(
              'Her gün ${MaterialLocalizations.of(context).formatTimeOfDay(TimeOfDay(hour: reminders.hour, minute: reminders.minute), alwaysUse24HourFormat: true)}; '
              'günün hedefi tutunca o günkü gelmez.',
            ),
            value: reminders.daily,
            onChanged: (v) =>
                setState(() => _reminders = reminders.copyWith(daily: v)),
          ),
          if (reminders.daily)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                icon: const Icon(Icons.schedule),
                label: const Text('Saati değiştir'),
                onPressed: () => _pickTime(reminders),
              ),
            ),
          SwitchListTile(
            key: const Key('reminder-weekly-switch'),
            contentPadding: EdgeInsets.zero,
            title: const Text('Haftalık plan hatırlatması'),
            subtitle: const Text('Pazartesi 09:00'),
            value: reminders.weeklyPlan,
            onChanged: (v) =>
                setState(() => _reminders = reminders.copyWith(weeklyPlan: v)),
          ),
          if (_error case final error?) ...[
            const SizedBox(height: AppSpacing.md),
            Text(
              error,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ],
          const SizedBox(height: AppSpacing.xl),
          NButton(
            label: 'Kaydet',
            expand: true,
            onPressed: _saving || weekly == null ? null : _save,
          ),
        ],
      ),
    );
  }
}
