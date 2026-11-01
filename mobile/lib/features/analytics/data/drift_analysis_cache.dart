import '../../../core/database/cache_store.dart';
import '../domain/analysis.dart';
import '../domain/analysis_repository.dart';

class DriftAnalysisCache implements AnalysisCache {
  DriftAnalysisCache(this._store);

  static String keyFor(String userId) => 'analysis_overview:$userId';

  final CacheStore _store;

  @override
  Future<AnalysisOverview?> read(String userId) async {
    final json = await _store.read(keyFor(userId));
    try {
      return json == null ? null : AnalysisOverview.fromJson(json);
    } on Object {
      return null; // written by an older app version; download again
    }
  }

  @override
  Future<void> write(String userId, AnalysisOverview overview) =>
      _store.write(keyFor(userId), overview.toJson());
}
