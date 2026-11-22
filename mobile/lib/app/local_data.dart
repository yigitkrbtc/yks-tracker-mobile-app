import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/database/cache_store.dart';
import '../core/database/database_providers.dart';
import '../core/notifications/notification_providers.dart';
import '../core/sync/sync_providers.dart';
import '../features/auth/application/session_controller.dart';
import '../features/goals/application/goal_providers.dart';
import '../features/profile/application/profile_providers.dart';

/// The student whose ownership of the device database has been checked since they signed in; null until then
/// and after sign-out or session expiry. Written only by [LocalData].
final verifiedOwnerProvider = NotifierProvider<VerifiedOwner, String?>(
  VerifiedOwner.new,
);

class VerifiedOwner extends Notifier<String?> {
  @override
  String? build() => null;

  void verify(String userId) => state = userId;

  void clear() => state = null;
}

/// True when someone is signed in and the device database is verified to be theirs (another student's rows are
/// wiped first). The router shows no screen before this, so no feature reads or syncs the previous student's data.
final localDataReadyProvider = Provider<bool>((ref) {
  final session = ref.watch(sessionControllerProvider).value;
  final verified = ref.watch(verifiedOwnerProvider);
  return session is SignedInState && session.profile.id == verified;
});

/// Keeps the device database to one student and runs sync only while someone is signed in. Watched by the app
/// root so it lives as long as the app.
final localDataProvider = Provider<LocalData>((ref) {
  final data = LocalData(ref);
  ref.onDispose(data._cancelPending);
  ref.listen(sessionControllerProvider, (_, next) {
    final session = next.value;
    if (session is SignedInState) {
      data._signedIn(session.profile.id);
    } else if (session is SignedOutState) {
      data._signedOut();
    }
  }, fireImmediately: true);
  return data;
});

class LocalData {
  LocalData(this._ref);

  static const ownerKey = 'owner';

  final Ref _ref;

  /// Counts ownership checks; one that finds itself outdated (sign-out, another account) stops quietly.
  int _attempt = 0;

  void _cancelPending() => _attempt++;

  /// Signs out and removes the student's data from this device (K6). Queued writes that never reached the
  /// server are lost, so the caller warns first.
  Future<void> signOutAndWipe() async {
    _ref.read(syncSchedulerProvider).stop();
    await _ref.read(sessionControllerProvider.notifier).logout();
    await _ref.read(appDatabaseProvider).wipe();
    await _clearReminders();
  }

  /// Reminders belong to the student who set them; settings went with the wipe.
  Future<void> _clearReminders() async {
    await _ref.read(localReminderSchedulerProvider).replaceAll(const []);
    _ref.invalidate(reminderSettingsProvider);
  }

  /// Session expiry keeps the data on purpose (the same student's unsent writes), but nobody is verified then.
  void _signedOut() {
    _cancelPending();
    _ref.read(syncSchedulerProvider).stop();
    if (_ref.read(verifiedOwnerProvider) != null) {
      _ref.read(verifiedOwnerProvider.notifier).clear();
    }
  }

  void _report(Object e, StackTrace s) => FlutterError.reportError(
    FlutterErrorDetails(exception: e, stack: s, library: 'local data'),
  );

  /// Makes the device database [userId]'s; false when this check went stale.
  Future<bool> _claimDevice(String userId, int attempt) async {
    final db = _ref.read(appDatabaseProvider);
    final cache = CacheStore(db);
    final owner = (await cache.read(ownerKey))?['userId'];
    if (attempt != _attempt) {
      return false;
    }
    if (owner != userId) {
      // Another account used this device last (or nobody did): start from an empty database. A sync still
      // running for that account is abandoned first so it cannot write after the wipe.
      _ref.read(syncSchedulerProvider).stop();
      await db.wipe();
      if (attempt != _attempt) {
        return false;
      }
      await cache.write(ownerKey, {'userId': userId});
      try {
        await _clearReminders();
      } on Object catch (e, s) {
        // Reminders are not a data isolation gate; carry on verifying.
        _report(e, s);
      }
      if (attempt != _attempt) {
        return false;
      }
      // The profile may have been read from the previous student's cache while this ran.
      _ref.invalidate(studyProfileProvider);
    }
    return true;
  }

  /// Marks [userId] verified only once the database is theirs; sync starts after that and not before.
  Future<void> _signedIn(String userId) async {
    final attempt = ++_attempt;
    final verified = _ref.read(verifiedOwnerProvider.notifier);
    if (_ref.read(verifiedOwnerProvider) != userId) {
      _ref.read(syncSchedulerProvider).stop();
    }
    try {
      if (!await _claimDevice(userId, attempt)) {
        return;
      }
    } on Object catch (e, s) {
      if (attempt == _attempt) {
        _report(e, s);
        // Back to the sign-in screen with nothing of the previous student
        // shown; the next sign-in retries the wipe.
        await _ref.read(sessionControllerProvider.notifier).logout();
      }
      return;
    }
    verified.verify(userId);
    _ref.read(syncSchedulerProvider).start();
  }
}
