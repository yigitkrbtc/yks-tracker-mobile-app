import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../profile/application/profile_providers.dart';
import '../domain/curriculum.dart';
import '../domain/curriculum_repository.dart';

/// Bound to the HTTP implementation in `app/composition.dart`; tests bind fakes.
final curriculumRepositoryProvider = Provider<CurriculumRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

/// Bound to the on-device cache in `app/composition.dart`; defaults to no cache.
final curriculumCacheProvider = Provider<CurriculumCache>(
  (ref) => const NoCurriculumCache(),
);

class NoCurriculumCache implements CurriculumCache {
  const NoCurriculumCache();

  @override
  Future<Curriculum?> read() async => null;

  @override
  Future<void> write(Curriculum curriculum) async {}
}

final curriculumProvider =
    AsyncNotifierProvider<CurriculumController, Curriculum>(
      CurriculumController.new,
    );

/// The curriculum: the device copy first (offline start), then revalidated against the server by version.
class CurriculumController extends AsyncNotifier<Curriculum> {
  @override
  Future<Curriculum> build() async {
    final cached = await ref.read(curriculumCacheProvider).read();
    if (cached != null) {
      // After build has published the cached copy; a failure keeps it.
      unawaited(Future(() => _revalidate(cached.version)));
      return cached;
    }
    final fresh = (await ref.read(curriculumRepositoryProvider).fetch())!;
    await ref.read(curriculumCacheProvider).write(fresh);
    return fresh;
  }

  /// Re-downloads only when the server's version differs; keeps the old copy on failure.
  Future<void> refresh() => _revalidate(state.value?.version);

  Future<void> _revalidate(String? version) async {
    try {
      final fresh = await ref
          .read(curriculumRepositoryProvider)
          .fetch(ifNoneMatch: version);
      if (fresh != null) {
        await ref.read(curriculumCacheProvider).write(fresh);
        state = AsyncData(fresh);
      }
    } on CurriculumException catch (e, stack) {
      if (state.value == null) {
        state = AsyncError(e, stack);
      }
    }
  }
}

/// Subjects of the student's score types; loading until both curriculum and profile are known.
final visibleSubjectsProvider = Provider<AsyncValue<List<Subject>>>((ref) {
  final curriculum = ref.watch(curriculumProvider);
  final profile = ref.watch(studyProfileProvider);
  for (final source in [curriculum, profile]) {
    if (source.hasError && source.value == null) {
      return AsyncError(source.error!, source.stackTrace!);
    }
  }
  final value = curriculum.value;
  final scoreTypes = profile.value?.scoreTypes;
  if (value == null || scoreTypes == null) {
    return const AsyncLoading();
  }
  return AsyncData(value.subjectsFor(scoreTypes.map((t) => t.code)));
});
