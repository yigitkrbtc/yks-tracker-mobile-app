import 'package:dio/dio.dart';

import '../../../core/network/api_problem.dart';
import '../domain/auth_failure.dart';
import '../domain/auth_models.dart';
import '../domain/auth_repository.dart';
import '../domain/legal_documents.dart';

class HttpAuthRepository implements AuthRepository {
  HttpAuthRepository(this._dio);

  final Dio _dio;

  @override
  Future<void> register(Registration r) => _post('/v1/auth/register', {
    'firstName': r.firstName,
    'lastName': r.lastName,
    'email': r.email,
    'password': r.password,
    'birthYear': r.birthYear,
    'termsVersion': LegalDocuments.termsVersion,
    'privacyNoticeVersion': LegalDocuments.privacyNoticeVersion,
    // No marketing messages exist yet; the opt-in appears once they do (adults only).
    'marketingConsent': false,
  });

  @override
  Future<SignInOutcome> verifyEmail(String email, String code) async => _signIn(
    await _post('/v1/auth/email/verify', {'email': email, 'code': code}),
  );

  @override
  Future<void> resendVerificationCode(String email) =>
      _post('/v1/auth/email/resend', {'email': email});

  @override
  Future<SignInOutcome> login(String email, String password) async => _signIn(
    await _post('/v1/auth/login', {'email': email, 'password': password}),
  );

  @override
  Future<AuthTokens> refresh(String refreshToken) async =>
      _tokens(await _post('/v1/auth/refresh', {'refreshToken': refreshToken}));

  @override
  Future<void> logout(String refreshToken) =>
      _post('/v1/auth/logout', {'refreshToken': refreshToken});

  @override
  Future<void> forgotPassword(String email) =>
      _post('/v1/auth/password/forgot', {'email': email});

  @override
  Future<void> resetPassword(String email, String code, String newPassword) =>
      _post('/v1/auth/password/reset', {
        'email': email,
        'code': code,
        'newPassword': newPassword,
      });

  @override
  Future<SignInOutcome> restoreAccount(String email, String password) async =>
      _signIn(
        await _post('/v1/auth/account/restore', {
          'email': email,
          'password': password,
        }),
      );

  @override
  Future<UserProfile> me(String accessToken) async {
    final body = await _send(
      () => _dio.get<Map<String, dynamic>>(
        '/v1/me',
        options: _bearer(accessToken),
      ),
    );
    return UserProfile.fromJson(body!);
  }

  @override
  Future<DateTime> requestDeletion(String accessToken, String password) async {
    final body = await _send(
      () => _dio.post<Map<String, dynamic>>(
        '/v1/me/deletion',
        data: {'password': password},
        options: _bearer(accessToken),
      ),
    );
    return DateTime.parse(body!['purgeAt'] as String);
  }

  Future<Map<String, dynamic>?> _post(String path, Map<String, Object> data) =>
      _send(() => _dio.post<Map<String, dynamic>>(path, data: data));

  Future<Map<String, dynamic>?> _send(
    Future<Response<Map<String, dynamic>>> Function() call,
  ) async {
    try {
      return (await call()).data;
    } on DioException catch (e) {
      final problem = ApiProblem.fromDio(e);
      throw AuthException(
        AuthFailure.fromProblemType(problem.type, problem.status),
        fieldErrors: problem.fieldErrors,
      );
    }
  }

  static Options _bearer(String token) =>
      Options(headers: {'Authorization': 'Bearer $token'});

  static SignInOutcome _signIn(Map<String, dynamic>? body) {
    final tokens = body?['tokens'];
    if (tokens is Map<String, dynamic>) {
      return SignedIn(_tokens(tokens));
    }
    return const SecondFactorRequired();
  }

  static AuthTokens _tokens(Map<String, dynamic>? json) => AuthTokens(
    accessToken: json!['accessToken'] as String,
    accessTokenExpiresAt: DateTime.parse(
      json['accessTokenExpiresAt'] as String,
    ),
    refreshToken: json['refreshToken'] as String,
  );
}
