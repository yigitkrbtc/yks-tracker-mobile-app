import '../../../core/database/cache_store.dart';
import '../domain/goal_repositories.dart';
import '../domain/reminders.dart';

/// Reminder choices in the device cache; removed with the rest of the student's data on sign-out.
class DriftReminderSettingsStore implements ReminderSettingsStore {
  DriftReminderSettingsStore(this._store);

  static const key = 'reminder_settings';

  final CacheStore _store;

  @override
  Future<ReminderSettings> read() async {
    final json = await _store.read(key);
    return json == null
        ? const ReminderSettings()
        : ReminderSettings.fromJson(json);
  }

  @override
  Future<void> write(ReminderSettings settings) =>
      _store.write(key, settings.toJson());
}
