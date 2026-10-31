import '../../../core/database/cache_store.dart';
import '../domain/curriculum.dart';
import '../domain/curriculum_repository.dart';

class DriftCurriculumCache implements CurriculumCache {
  DriftCurriculumCache(this._store);

  static const key = 'curriculum';

  final CacheStore _store;

  @override
  Future<Curriculum?> read() async {
    final json = await _store.read(key);
    try {
      return json == null ? null : Curriculum.fromJson(json);
    } on Object {
      return null; // written by an older app version; download again
    }
  }

  @override
  Future<void> write(Curriculum curriculum) =>
      _store.write(key, curriculum.toJson());
}
