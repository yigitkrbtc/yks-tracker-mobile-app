/// One reminder shown by the device at [at] (UTC). Features build these; core knows no feature.
class LocalReminder {
  const LocalReminder({
    required this.id,
    required this.at,
    required this.title,
    required this.body,
  });

  final int id;
  final DateTime at;
  final String title;
  final String body;

  @override
  bool operator ==(Object other) =>
      other is LocalReminder &&
      other.id == id &&
      other.at == at &&
      other.title == title &&
      other.body == body;

  @override
  int get hashCode => Object.hash(id, at, title, body);

  @override
  String toString() => 'LocalReminder($id, $at, $title)';
}

/// Port for on-device notifications (K11: local only, no push). Bound in `app/composition.dart`.
abstract interface class LocalReminderScheduler {
  /// Asks for the notification permission where the OS needs it (Android 13+); true when allowed.
  Future<bool> requestPermission();

  /// Cancels every reminder this app scheduled, then schedules [reminders].
  Future<void> replaceAll(List<LocalReminder> reminders);
}

/// Default when nothing is bound (tests, unsupported platforms): schedules nothing.
class NoReminderScheduler implements LocalReminderScheduler {
  const NoReminderScheduler();

  @override
  Future<bool> requestPermission() async => false;

  @override
  Future<void> replaceAll(List<LocalReminder> reminders) async {}
}
