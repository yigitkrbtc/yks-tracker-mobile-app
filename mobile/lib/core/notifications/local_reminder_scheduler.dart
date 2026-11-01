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

/// Runs [replaceAll] calls one after another, in call order, so a cancel-then-schedule of one call never
/// interleaves with another's and the last call always wins. Wraps the platform adapter.
class SerialReminderScheduler implements LocalReminderScheduler {
  SerialReminderScheduler(this._inner);

  final LocalReminderScheduler _inner;
  Future<void> _tail = Future.value();

  @override
  Future<bool> requestPermission() => _inner.requestPermission();

  @override
  Future<void> replaceAll(List<LocalReminder> reminders) {
    final next = _tail.then((_) => _inner.replaceAll(reminders));
    // A failed call must not block the ones after it.
    _tail = next.catchError((Object _) {});
    return next;
  }
}
