import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:drift/drift.dart' show driftRuntimeOptions;
import 'package:drift/native.dart';
import 'package:nirengi/core/database/app_database.dart';

/// In-memory SQLite with the app's schema.
AppDatabase memoryDatabase() {
  driftRuntimeOptions.dontWarnAboutMultipleDatabases = true;
  return AppDatabase(NativeDatabase.memory());
}

typedef FakeRoute = ResponseBody Function(RequestOptions request);

/// Stands in for the API at the HTTP adapter level; unrouted calls answer an empty change feed.
class FakeApi implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  FakeRoute? route;

  Dio get dio =>
      Dio(BaseOptions(baseUrl: 'http://api.test'))..httpClientAdapter = this;

  Iterable<String> get calls => requests.map(
    (r) =>
        '${r.method} ${r.uri.path}${r.uri.hasQuery ? '?${r.uri.query}' : ''}',
  );

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    final handler = route;
    if (handler != null) {
      return handler(options);
    }
    return json(200, {'topicTests': <Object>[], 'cursor': 0, 'hasMore': false});
  }

  @override
  void close({bool force = false}) {}

  static ResponseBody json(int status, Object? body) => ResponseBody.fromString(
    body == null ? '' : jsonEncode(body),
    status,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  static Never offline(RequestOptions options) => throw DioException(
    requestOptions: options,
    type: DioExceptionType.connectionError,
  );
}
