import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/sync/sync_providers.dart';
import '../../auth/application/session_controller.dart';
import '../../profile/application/profile_providers.dart';
import '../domain/recommendation_repository.dart';
import '../domain/weekly_plan.dart';

/// Bound to the HTTP implementation in `app/composition.dart`; tests bind fakes.
final recommendationRepositoryProvider = Provider<RecommendationRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

/// Bound to the device cache in `app/composition.dart`; defaults to none.
final recommendationCacheProvider = Provider<RecommendationCache>(
  (ref) => const NoRecommendationCache(),
);

class NoRecommendationCache implements RecommendationCache {
  const NoRecommendationCache();

  @override
  Future<WeeklyPlan?> read(String userId) async => null;

  @override
  Future<void> write(String userId, WeeklyPlan plan) async {}
}

final weeklyPlanProvider =
    AsyncNotifierProvider<WeeklyPlanController, WeeklyPlan?>(
      WeeklyPlanController.new,
    );

/// This week's plan (null when signed out): the device copy first, then revalidated with its ETag; again after
/// each complete sync, since progress comes from the server's records.
class WeeklyPlanController extends AsyncNotifier<WeeklyPlan?> {
  String? _userId;

  @override
  Future<WeeklyPlan?> build() async {
    final session = await ref.watch(sessionControllerProvider.future);
    if (session is! SignedInState) {
      return _userId = null;
    }
    final userId = _userId = session.profile.id;
    final synced = ref
        .watch(syncServiceProvider)
        .completed
        .listen((_) => unawaited(refresh()));
    ref.onDispose(synced.cancel);
    ref.listen(studyProfileProvider, (previous, next) {
      if (previous?.value != null && next.value != previous?.value) {
        unawaited(refresh());
      }
    });

    final cached = await ref.read(recommendationCacheProvider).read(userId);
    if (cached != null) {
      unawaited(Future(() => _revalidate(userId, cached.etag)));
      return cached;
    }
    final fresh = (await ref
        .read(recommendationRepositoryProvider)
        .fetchWeek())!;
    await ref.read(recommendationCacheProvider).write(userId, fresh);
    return fresh;
  }

  Future<void> refresh() async {
    final userId = _userId;
    if (userId != null) {
      await _revalidate(userId, state.value?.etag);
    }
  }

  /// Online only (K6). Throws [RecommendationException]; the plan keeps its item on failure.
  Future<void> dismiss(PlanItem item, DismissReason reason) async {
    final userId = _userId;
    if (userId == null) {
      return;
    }
    final plan = await ref
        .read(recommendationRepositoryProvider)
        .dismiss(item.key, reason);
    await ref.read(recommendationCacheProvider).write(userId, plan);
    if (userId == _userId) {
      state = AsyncData(plan);
    }
  }

  Future<void> _revalidate(String userId, String? etag) async {
    try {
      final fresh = await ref
          .read(recommendationRepositoryProvider)
          .fetchWeek(ifNoneMatch: etag);
      if (fresh != null && userId == _userId) {
        await ref.read(recommendationCacheProvider).write(userId, fresh);
        state = AsyncData(fresh);
      }
    } on RecommendationException catch (e, stack) {
      if (state.value == null && userId == _userId) {
        state = AsyncError(e, stack);
      }
    }
  }
}
