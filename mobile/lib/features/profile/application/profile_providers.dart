import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../auth/application/session_controller.dart';
import '../domain/profile_repository.dart';
import '../domain/study_profile.dart';

/// Bound to the HTTP implementation in `app/composition.dart`; tests bind fakes.
final profileRepositoryProvider = Provider<ProfileRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

final studyProfileProvider =
    AsyncNotifierProvider<StudyProfileController, StudyProfile?>(
      StudyProfileController.new,
    );

/// The signed-in student's study profile; null means onboarding is still to do.
class StudyProfileController extends AsyncNotifier<StudyProfile?> {
  @override
  Future<StudyProfile?> build() async {
    final session = await ref.watch(sessionControllerProvider.future);
    if (session is! SignedInState) {
      return null;
    }
    return ref.read(profileRepositoryProvider).get();
  }

  /// Throws [ProfileException]; on success the new profile is the state.
  Future<void> save(StudyProfile profile) async {
    final saved = await ref.read(profileRepositoryProvider).save(profile);
    state = AsyncData(saved);
  }
}
