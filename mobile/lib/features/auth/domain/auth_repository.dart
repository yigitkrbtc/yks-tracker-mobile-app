import 'auth_models.dart';

/// Identity API. Every method throws `AuthException` on failure.
abstract interface class AuthRepository {
  Future<void> register(Registration registration);

  Future<SignInOutcome> verifyEmail(String email, String code);

  Future<void> resendVerificationCode(String email);

  Future<SignInOutcome> login(String email, String password);

  Future<AuthTokens> refresh(String refreshToken);

  Future<void> logout(String refreshToken);

  Future<void> forgotPassword(String email);

  Future<void> resetPassword(String email, String code, String newPassword);

  Future<SignInOutcome> restoreAccount(String email, String password);

  Future<UserProfile> me(String accessToken);

  /// Returns when the account will be purged unless restored.
  Future<DateTime> requestDeletion(String accessToken, String password);
}

/// Device storage for what survives an app restart: refresh token and last known profile.
abstract interface class SessionStore {
  Future<String?> readRefreshToken();

  Future<UserProfile?> readProfile();

  Future<void> save({required String refreshToken, UserProfile? profile});

  Future<void> clear();
}
