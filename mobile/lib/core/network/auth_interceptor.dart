import 'package:dio/dio.dart';

import 'session_tokens.dart';

/// Adds the bearer token and, on a 401, refreshes once and replays the request.
class AuthInterceptor extends QueuedInterceptor {
  AuthInterceptor(this._tokens, this._dio);

  static const _retried = 'auth_retried';

  final SessionTokens _tokens;
  final Dio _dio;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final token = _tokens.accessToken;
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final request = err.requestOptions;
    if (err.response?.statusCode != 401 || request.extra[_retried] == true) {
      handler.next(err);
      return;
    }

    final token = await _tokens.refreshAccessToken();
    if (token == null) {
      handler.next(err);
      return;
    }

    request.extra[_retried] = true;
    request.headers['Authorization'] = 'Bearer $token';
    try {
      handler.resolve(await _dio.fetch<dynamic>(request));
    } on DioException catch (retryError) {
      handler.next(retryError);
    }
  }
}
