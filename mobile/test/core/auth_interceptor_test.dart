import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/network/auth_interceptor.dart';
import 'package:nirengi/core/network/session_tokens.dart';

class _Tokens implements SessionTokens {
  @override
  String? accessToken = 'old';
  var refreshes = 0;

  @override
  Future<String?> refreshAccessToken() async {
    refreshes++;
    return accessToken = 'new';
  }
}

/// Accepts only the "new" token, like an API whose access token just expired.
class _Server implements HttpClientAdapter {
  final seen = <String?>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<List<int>>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    final auth = options.headers['Authorization'] as String?;
    seen.add(auth);
    final ok = auth == 'Bearer new';
    return ResponseBody.fromString(
      jsonEncode({'ok': ok}),
      ok ? 200 : 401,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  test(
    '401 triggers one refresh and the request is replayed with the new token',
    () async {
      final tokens = _Tokens();
      final server = _Server();
      final dio = Dio()..httpClientAdapter = server;
      dio.interceptors.add(AuthInterceptor(tokens, dio));

      final response = await dio.get<Map<String, dynamic>>(
        'https://api.test/v1/me',
      );

      expect(response.data, {'ok': true});
      expect(tokens.refreshes, 1);
      expect(server.seen, ['Bearer old', 'Bearer new']);
    },
  );
}
