import 'package:flutter_riverpod/misc.dart';
import 'package:nirengi/app/composition.dart';
import 'package:nirengi/features/auth/application/auth_providers.dart';
import 'package:nirengi/features/auth/domain/auth_failure.dart';
import 'package:nirengi/features/auth/domain/auth_models.dart';
import 'package:nirengi/features/auth/domain/auth_repository.dart';

const testProfile = UserProfile(
  id: '0199a000-0000-7000-8000-000000000001',
  firstName: 'Ada',
  lastName: 'Yılmaz',
  email: 'ada@example.com',
  birthYear: 2008,
  canGiveMarketingConsent: false,
);

AuthTokens testTokens(String suffix, {DateTime? expiresAt}) => AuthTokens(
  accessToken: 'access-$suffix',
  accessTokenExpiresAt:
      expiresAt ?? DateTime.now().add(const Duration(minutes: 15)),
  refreshToken: 'refresh-$suffix',
);

/// Scriptable [AuthRepository]: set the next outcome per call, record what was called.
class FakeAuthRepository implements AuthRepository {
  final calls = <String>[];
  SignInOutcome loginOutcome = SignedIn(testTokens('login'));
  AuthException? loginError;
  AuthException? refreshError;
  AuthException? meError;
  var refreshCount = 0;

  @override
  Future<void> register(Registration registration) async =>
      calls.add('register:${registration.email}:${registration.birthYear}');

  @override
  Future<SignInOutcome> verifyEmail(String email, String code) async {
    calls.add('verify:$email:$code');
    return SignedIn(testTokens('verify'));
  }

  @override
  Future<void> resendVerificationCode(String email) async =>
      calls.add('resend:$email');

  @override
  Future<SignInOutcome> login(String email, String password) async {
    calls.add('login:$email');
    if (loginError != null) {
      throw loginError!;
    }
    return loginOutcome;
  }

  @override
  Future<AuthTokens> refresh(String refreshToken) async {
    calls.add('refresh:$refreshToken');
    refreshCount++;
    await Future<void>.delayed(const Duration(milliseconds: 5));
    if (refreshError != null) {
      throw refreshError!;
    }
    return testTokens('r$refreshCount');
  }

  @override
  Future<void> logout(String refreshToken) async =>
      calls.add('logout:$refreshToken');

  @override
  Future<void> forgotPassword(String email) async => calls.add('forgot:$email');

  @override
  Future<void> resetPassword(
    String email,
    String code,
    String newPassword,
  ) async => calls.add('reset:$email:$code');

  @override
  Future<SignInOutcome> restoreAccount(String email, String password) async {
    calls.add('restore:$email');
    return SignedIn(testTokens('restore'));
  }

  @override
  Future<UserProfile> me(String accessToken) async {
    calls.add('me:$accessToken');
    if (meError != null) {
      throw meError!;
    }
    return testProfile;
  }

  @override
  Future<DateTime> requestDeletion(String accessToken, String password) async {
    calls.add('delete:$accessToken');
    return DateTime.utc(2026, 11, 26);
  }
}

class MemorySessionStore implements SessionStore {
  MemorySessionStore({this.refreshToken, this.profile});

  String? refreshToken;
  UserProfile? profile;

  @override
  Future<String?> readRefreshToken() async => refreshToken;

  @override
  Future<UserProfile?> readProfile() async => profile;

  @override
  Future<void> save({
    required String refreshToken,
    UserProfile? profile,
  }) async {
    this.refreshToken = refreshToken;
    this.profile = profile ?? this.profile;
  }

  @override
  Future<void> clear() async {
    refreshToken = null;
    profile = null;
  }
}

List<Override> fakeAuthOverrides(
  FakeAuthRepository repository,
  MemorySessionStore store,
) => [
  authRepositoryProvider.overrideWithValue(repository),
  sessionStoreProvider.overrideWithValue(store),
  ...sessionOverrides,
];

const networkError = AuthException(AuthFailure.network);
const expiredSession = AuthException(AuthFailure.sessionExpired);
