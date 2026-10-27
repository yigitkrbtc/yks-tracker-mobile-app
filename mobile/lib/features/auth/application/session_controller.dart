import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/auth_failure.dart';
import '../domain/auth_models.dart';
import 'auth_providers.dart';

sealed class SessionState {
  const SessionState();
}

class SignedOutState extends SessionState {
  const SignedOutState();
}

class SignedInState extends SessionState {
  const SignedInState(this.profile);

  final UserProfile profile;
}

final sessionControllerProvider =
    AsyncNotifierProvider<SessionController, SessionState>(
      SessionController.new,
    );

/// The app's sign-in state. Starts by restoring the stored session; offline it falls back
/// to the last known profile so the app stays usable (offline-first).
class SessionController extends AsyncNotifier<SessionState> {
  @override
  Future<SessionState> build() async {
    final tokens = ref.watch(sessionTokenManagerProvider);
    tokens.onSessionExpired = () => state = const AsyncData(SignedOutState());

    final store = ref.watch(sessionStoreProvider);
    if (await store.readRefreshToken() == null) {
      return const SignedOutState();
    }

    try {
      final access = await tokens.refresh();
      final profile = await ref.read(authRepositoryProvider).me(access);
      await store.save(refreshToken: tokens.refreshToken!, profile: profile);
      return SignedInState(profile);
    } on AuthException catch (e) {
      if (e.failure == AuthFailure.network) {
        final cached = await store.readProfile();
        if (cached != null) {
          return SignedInState(cached);
        }
      } else {
        await tokens.clear();
      }
      return const SignedOutState();
    }
  }

  Future<void> login(String email, String password) async =>
      _complete(await ref.read(authRepositoryProvider).login(email, password));

  Future<void> verifyEmail(String email, String code) async => _complete(
    await ref.read(authRepositoryProvider).verifyEmail(email, code),
  );

  Future<void> restoreAccount(String email, String password) async => _complete(
    await ref.read(authRepositoryProvider).restoreAccount(email, password),
  );

  Future<void> logout() async {
    final tokens = ref.read(sessionTokenManagerProvider);
    final refreshToken = tokens.refreshToken;
    if (refreshToken != null) {
      try {
        await ref.read(authRepositoryProvider).logout(refreshToken);
      } on AuthException {
        // Signing out locally must work offline; the server session expires on its own.
      }
    }
    await tokens.clear();
    state = const AsyncData(SignedOutState());
  }

  /// Starts the 30-day deletion grace period and signs out; returns the purge date.
  Future<DateTime> deleteAccount(String password) async {
    final tokens = ref.read(sessionTokenManagerProvider);
    final purgeAt = await ref
        .read(authRepositoryProvider)
        .requestDeletion(await tokens.freshAccessToken(), password);
    await tokens.clear();
    state = const AsyncData(SignedOutState());
    return purgeAt;
  }

  Future<void> _complete(SignInOutcome outcome) async {
    switch (outcome) {
      case SecondFactorRequired():
        throw const AuthException(AuthFailure.secondFactorRequired);
      case SignedIn(:final tokens):
        final profile = await ref
            .read(authRepositoryProvider)
            .me(tokens.accessToken);
        await ref
            .read(sessionTokenManagerProvider)
            .adopt(tokens, profile: profile);
        state = AsyncData(SignedInState(profile));
    }
  }
}
