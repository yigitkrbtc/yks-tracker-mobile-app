import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../auth/application/session_controller.dart';
import '../domain/profile_repository.dart';
import '../domain/study_profile.dart';

/// Bound to the HTTP implementation in `app/composition.dart`; tests bind fakes.
final profileRepositoryProvider = Provider<ProfileRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

/// Bound to the on-device cache in `app/composition.dart`; defaults to no cache.
final profileCacheProvider = Provider<ProfileCache>(
  (ref) => const NoProfileCache(),
);

class NoProfileCache implements ProfileCache {
  const NoProfileCache();

  @override
  Future<StudyProfile?> read() async => null;

  @override
  Future<void> write(StudyProfile? profile) async {}
}

final studyProfileProvider =
    AsyncNotifierProvider<StudyProfileController, StudyProfile?>(
      StudyProfileController.new,
    );

/// The signed-in student's study profile; null means onboarding is still to do. Offline, the device copy.
class StudyProfileController extends AsyncNotifier<StudyProfile?> {
  @override
  Future<StudyProfile?> build() async {
    final session = await ref.watch(sessionControllerProvider.future);
    if (session is! SignedInState) {
      return null;
    }
    final cache = ref.read(profileCacheProvider);
    try {
      final profile = await ref.read(profileRepositoryProvider).get();
      await cache.write(profile);
      return profile;
    } on ProfileException catch (e) {
      final cached = e.isNetworkError ? await cache.read() : null;
      if (cached == null) {
        rethrow;
      }
      return cached;
    }
  }

  /// Throws [ProfileException]; on success the new profile is the state.
  Future<void> save(StudyProfile profile) async {
    final saved = await ref.read(profileRepositoryProvider).save(profile);
    await ref.read(profileCacheProvider).write(saved);
    state = AsyncData(saved);
  }
}
