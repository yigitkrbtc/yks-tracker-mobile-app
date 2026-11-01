import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../profile/application/profile_providers.dart';
import '../domain/exam_schedule.dart';
import '../domain/exam_schedule_repository.dart';

/// Bound to the HTTP implementation in `app/composition.dart`; tests bind fakes.
final examScheduleRepositoryProvider = Provider<ExamScheduleRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

/// Bound to the device cache in `app/composition.dart`; defaults to none.
final examScheduleCacheProvider = Provider<ExamScheduleCache>(
  (ref) => const NoExamScheduleCache(),
);

class NoExamScheduleCache implements ExamScheduleCache {
  const NoExamScheduleCache();

  @override
  Future<ExamSchedule?> read() async => null;

  @override
  Future<void> write(ExamSchedule schedule) async {}
}

final examScheduleProvider =
    AsyncNotifierProvider<ExamScheduleController, ExamSchedule>(
      ExamScheduleController.new,
    );

/// The calendar: the device copy first, then revalidated by version.
class ExamScheduleController extends AsyncNotifier<ExamSchedule> {
  @override
  Future<ExamSchedule> build() async {
    final cached = await ref.read(examScheduleCacheProvider).read();
    if (cached != null) {
      unawaited(Future(() => _revalidate(cached.version)));
      return cached;
    }
    final fresh = (await ref.read(examScheduleRepositoryProvider).fetch())!;
    await ref.read(examScheduleCacheProvider).write(fresh);
    return fresh;
  }

  Future<void> _revalidate(String version) async {
    try {
      final fresh = await ref
          .read(examScheduleRepositoryProvider)
          .fetch(ifNoneMatch: version);
      if (fresh != null) {
        await ref.read(examScheduleCacheProvider).write(fresh);
        state = AsyncData(fresh);
      }
    } on ExamScheduleException {
      // Keep the device copy.
    }
  }
}

/// The calendar year of the student's target exam; null while unknown or not published yet.
final targetExamYearProvider = Provider<ExamYear?>((ref) {
  final year = ref.watch(studyProfileProvider).value?.targetExamYear;
  final schedule = ref.watch(examScheduleProvider).value;
  return year == null ? null : schedule?.year(year);
});
