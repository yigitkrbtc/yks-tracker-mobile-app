import '../../../core/database/cache_store.dart';
import '../domain/profile_repository.dart';
import '../domain/study_profile.dart';

class DriftProfileCache implements ProfileCache {
  DriftProfileCache(this._store);

  static const key = 'study_profile';

  final CacheStore _store;

  @override
  Future<StudyProfile?> read() async {
    final json = await _store.read(key);
    try {
      return json == null ? null : StudyProfile.fromJson(json);
    } on Object {
      return null;
    }
  }

  @override
  Future<void> write(StudyProfile? profile) => profile == null
      ? _store.remove(key)
      : _store.write(key, profile.toJson());
}
