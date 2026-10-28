import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../profile/application/profile_providers.dart';
import '../domain/curriculum.dart';
import '../domain/curriculum_repository.dart';

/// Bound to the HTTP implementation in `app/composition.dart`; tests bind fakes.
final curriculumRepositoryProvider = Provider<CurriculumRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

final curriculumProvider =
    AsyncNotifierProvider<CurriculumController, Curriculum>(
      CurriculumController.new,
    );

/// The curriculum, kept in memory and revalidated by version (persistent cache arrives with Drift, S3).
class CurriculumController extends AsyncNotifier<Curriculum> {
  @override
  Future<Curriculum> build() async =>
      (await ref.read(curriculumRepositoryProvider).fetch())!;

  /// Re-downloads only when the server's version differs; keeps the old copy on failure.
  Future<void> refresh() async {
    final current = state.value;
    try {
      final fresh = await ref
          .read(curriculumRepositoryProvider)
          .fetch(ifNoneMatch: current?.version);
      if (fresh != null) {
        state = AsyncData(fresh);
      }
    } on CurriculumException catch (e, stack) {
      if (current == null) {
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
