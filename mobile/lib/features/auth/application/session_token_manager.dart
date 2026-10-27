import '../../../core/network/session_tokens.dart';
import '../domain/auth_failure.dart';
import '../domain/auth_models.dart';
import '../domain/auth_repository.dart';

/// Owns the tokens: access token in memory, refresh token in the [SessionStore].
/// Refreshes are single-flight, because the API treats a reused refresh token as theft.
class SessionTokenManager implements SessionTokens {
  SessionTokenManager(
    this._repository,
    this._store, {
    DateTime Function()? clock,
  }) : _clock = clock ?? DateTime.now;

  static const _expiryMargin = Duration(seconds: 30);

  final AuthRepository _repository;
  final SessionStore _store;
  final DateTime Function() _clock;

  AuthTokens? _tokens;
  Future<String>? _inFlight;

  /// Called when the server rejects the session (expired, revoked, reused).
  void Function()? onSessionExpired;

  @override
  String? get accessToken => _tokens?.accessToken;

  String? get refreshToken => _tokens?.refreshToken;

  Future<void> adopt(AuthTokens tokens, {UserProfile? profile}) async {
    _tokens = tokens;
    await _store.save(refreshToken: tokens.refreshToken, profile: profile);
  }

  /// A usable access token, refreshing first if it is about to expire.
  Future<String> freshAccessToken() async {
    final tokens = _tokens;
    if (tokens != null && !tokens.expiresWithin(_expiryMargin, _clock())) {
      return tokens.accessToken;
    }
    return refresh();
  }

  /// Throws [AuthException]; `sessionExpired` also clears the stored session.
  Future<String> refresh() =>
      _inFlight ??= _refresh().whenComplete(() => _inFlight = null);

  @override
  Future<String?> refreshAccessToken() async {
    try {
      return await refresh();
    } on AuthException {
      return null;
    }
  }

  Future<void> clear() async {
    _tokens = null;
    await _store.clear();
  }

  Future<String> _refresh() async {
    final current = _tokens?.refreshToken ?? await _store.readRefreshToken();
    if (current == null) {
      throw const AuthException(AuthFailure.sessionExpired);
    }

    try {
      final tokens = await _repository.refresh(current);
      await adopt(tokens);
      return tokens.accessToken;
    } on AuthException catch (e) {
      if (e.failure == AuthFailure.sessionExpired) {
        await clear();
        onSessionExpired?.call();
      }
      rethrow;
    }
  }
}
