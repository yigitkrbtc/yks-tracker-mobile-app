import 'package:dio/dio.dart';

import 'session_tokens.dart';

/// Adds the bearer token and, on a 401, refreshes once and replays the request.
///
/// The replay goes through [_retryClient], a client without this interceptor: replaying
/// through the queued client would park the replay's own error behind the current one and
/// deadlock every later request when the replay fails.
class AuthInterceptor extends QueuedInterceptor {
  AuthInterceptor(this._tokens, this._retryClient);

  static const _retried = 'auth_retried';

  final SessionTokens _tokens;
  final Dio _retryClient;

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
      handler.resolve(await _retryClient.fetch<dynamic>(request));
    } on DioException catch (retryError) {
      handler.next(retryError);
    }
  }
}
