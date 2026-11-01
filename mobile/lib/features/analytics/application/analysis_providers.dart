import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/sync/sync_providers.dart';
import '../../auth/application/session_controller.dart';
import '../../profile/application/profile_providers.dart';
import '../domain/analysis.dart';
import '../domain/analysis_repository.dart';

/// Bound to the HTTP implementation in `app/composition.dart`; tests bind fakes.
final analysisRepositoryProvider = Provider<AnalysisRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

/// Bound to the on-device cache in `app/composition.dart`; defaults to no cache.
final analysisCacheProvider = Provider<AnalysisCache>(
  (ref) => const NoAnalysisCache(),
);

class NoAnalysisCache implements AnalysisCache {
  const NoAnalysisCache();

  @override
  Future<AnalysisOverview?> read(String userId) async => null;

  @override
  Future<void> write(String userId, AnalysisOverview overview) async {}
}

final analysisOverviewProvider =
    AsyncNotifierProvider<AnalysisOverviewController, AnalysisOverview?>(
      AnalysisOverviewController.new,
    );

/// The signed-in student's analysis panel (null when signed out): the device copy first, then revalidated with
/// its ETag. Revalidated again after each complete sync (the server has every local record) and when the study
/// profile changes (ADR-0016).
class AnalysisOverviewController extends AsyncNotifier<AnalysisOverview?> {
  String? _userId;

  @override
  Future<AnalysisOverview?> build() async {
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

    final cached = await ref.read(analysisCacheProvider).read(userId);
    if (cached != null) {
      unawaited(Future(() => _revalidate(userId, cached.etag)));
      return cached;
    }
    final fresh = (await ref.read(analysisRepositoryProvider).fetchOverview())!;
    await ref.read(analysisCacheProvider).write(userId, fresh);
    return fresh;
  }

  /// Downloads only when the server's analysis changed; keeps the old copy on failure.
  Future<void> refresh() async {
    final userId = _userId;
    if (userId != null) {
      await _revalidate(userId, state.value?.etag);
    }
  }

  Future<void> _revalidate(String userId, String? etag) async {
    try {
      final fresh = await ref
          .read(analysisRepositoryProvider)
          .fetchOverview(ifNoneMatch: etag);
      if (fresh != null && userId == _userId) {
        await ref.read(analysisCacheProvider).write(userId, fresh);
        state = AsyncData(fresh);
      }
    } on AnalysisException catch (e, stack) {
      if (state.value == null && userId == _userId) {
        state = AsyncError(e, stack);
      }
    }
  }
}

/// Status of a topic for list badges; null while unknown or when the topic has no record.
final topicStatusProvider = Provider.family<TopicStatus?, String>(
  (ref, topicId) =>
      ref.watch(analysisOverviewProvider).value?.topicsById[topicId],
);

/// The topic card; re-fetched whenever the panel changes, so both always agree.
final topicAnalysisProvider = FutureProvider.autoDispose
    .family<TopicAnalysis, String>((ref, topicId) {
      ref.watch(analysisOverviewProvider.select((o) => o.value?.etag));
      return ref.read(analysisRepositoryProvider).fetchTopic(topicId);
    });
