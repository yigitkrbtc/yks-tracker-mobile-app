import 'study_profile.dart';

/// Study profile API. Every method throws [ProfileException] on failure.
abstract interface class ProfileRepository {
  /// Null until onboarding is completed.
  Future<StudyProfile?> get();

  Future<StudyProfile> save(StudyProfile profile);
}

class ProfileException implements Exception {
  const ProfileException(this.message, {this.isNetworkError = false});

  final String message;
  final bool isNetworkError;

  @override
  String toString() => 'ProfileException($message)';
}
