import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../config/app_config.dart';
import 'auth_interceptor.dart';
import 'session_tokens.dart';

Dio _dio() => Dio(
  BaseOptions(
    baseUrl: AppConfig.apiBaseUrl,
    connectTimeout: const Duration(seconds: 10),
    receiveTimeout: const Duration(seconds: 20),
    contentType: Headers.jsonContentType,
  ),
);

/// Client without credentials: sign-in, sign-up and token refresh.
final publicApiProvider = Provider<Dio>((ref) => _dio());

/// Client for everything behind sign-in; attaches and refreshes the access token.
final apiClientProvider = Provider<Dio>((ref) {
  final dio = _dio();
  dio.interceptors.add(AuthInterceptor(ref.watch(sessionTokensProvider), dio));
  return dio;
});
