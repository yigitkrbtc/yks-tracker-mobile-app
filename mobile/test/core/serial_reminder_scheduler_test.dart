import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/notifications/local_reminder_scheduler.dart';

/// Records the steps of each call; the first call is held open until released.
class _SlowScheduler implements LocalReminderScheduler {
  final steps = <String>[];
  final hold = Completer<void>();
  var _calls = 0;

  @override
  Future<bool> requestPermission() async => true;

  @override
  Future<void> replaceAll(List<LocalReminder> reminders) async {
    final call = _calls++;
    steps.add('cancel $call');
    if (call == 0) {
      await hold.future;
    }
    steps.add('schedule $call (${reminders.length})');
  }
}

void main() {
  final reminder = LocalReminder(
    id: 1,
    at: DateTime.utc(2026, 11, 4, 17),
    title: 't',
    body: 'b',
  );

  test('calls run one after another and the last one wins', () async {
    final inner = _SlowScheduler();
    final scheduler = SerialReminderScheduler(inner);

    final first = scheduler.replaceAll([reminder]);
    final clear = scheduler.replaceAll(const []);
    await Future<void>.delayed(Duration.zero);
    expect(inner.steps, ['cancel 0']);

    inner.hold.complete();
    await Future.wait([first, clear]);

    expect(inner.steps, [
      'cancel 0',
      'schedule 0 (1)',
      'cancel 1',
      'schedule 1 (0)',
    ]);
  });

  test('a failed call does not block the next one', () async {
    final scheduler = SerialReminderScheduler(_FailingOnce());

    await expectLater(scheduler.replaceAll([reminder]), throwsStateError);
    await scheduler.replaceAll(const []);
  });
}

class _FailingOnce implements LocalReminderScheduler {
  var _failed = false;

  @override
  Future<bool> requestPermission() async => true;

  @override
  Future<void> replaceAll(List<LocalReminder> reminders) async {
    if (!_failed) {
      _failed = true;
      throw StateError('plugin');
    }
  }
}
