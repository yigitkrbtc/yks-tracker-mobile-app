import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/database/cache_store.dart';
import '../core/database/database_providers.dart';
import '../core/sync/sync_providers.dart';
import '../features/auth/application/session_controller.dart';

/// Keeps the device database to one student and runs sync only while someone is signed in. Watched by the app
/// root so it lives as long as the app.
final localDataProvider = Provider<LocalData>((ref) {
  final data = LocalData(ref);
  ref.listen(sessionControllerProvider, (_, next) {
    final session = next.value;
    if (session is SignedInState) {
      data._signedIn(session.profile.id);
    } else if (session is SignedOutState) {
      ref.read(syncSchedulerProvider).stop();
    }
  }, fireImmediately: true);
  return data;
});

class LocalData {
  LocalData(this._ref);

  static const ownerKey = 'owner';

  final Ref _ref;

  /// Signs out and removes the student's data from this device (K6). Queued writes that never reached the
  /// server are lost, so the caller warns first.
  Future<void> signOutAndWipe() async {
    _ref.read(syncSchedulerProvider).stop();
    await _ref.read(sessionControllerProvider.notifier).logout();
    await _ref.read(appDatabaseProvider).wipe();
  }

  Future<void> _signedIn(String userId) async {
    final db = _ref.read(appDatabaseProvider);
    final cache = CacheStore(db);
    final owner = (await cache.read(ownerKey))?['userId'];
    if (owner != userId) {
      // Another account used this device last (or nobody did): start from an empty database. A sync still
      // running for that account is abandoned first so it cannot write after the wipe.
      _ref.read(syncSchedulerProvider).stop();
      await db.wipe();
      await cache.write(ownerKey, {'userId': userId});
    }
    _ref.read(syncSchedulerProvider).start();
  }
}
