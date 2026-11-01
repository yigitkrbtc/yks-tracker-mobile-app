import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'local_reminder_scheduler.dart';

/// Bound to the plugin in `app/composition.dart`; schedules nothing by default.
final localReminderSchedulerProvider = Provider<LocalReminderScheduler>(
  (ref) => const NoReminderScheduler(),
);
