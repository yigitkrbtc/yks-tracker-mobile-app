import 'reminders.dart';
import 'study_day.dart';

/// Questions per day from the device's records (not deleted), from [from] on.
abstract interface class ActivityRepository {
  Stream<List<DayActivity>> watchSince(DateTime from);
}

/// Reminder choices kept on this device only.
abstract interface class ReminderSettingsStore {
  Future<ReminderSettings> read();

  Future<void> write(ReminderSettings settings);
}
