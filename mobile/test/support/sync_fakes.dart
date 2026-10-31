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

/// Tracking endpoints in memory: stores what the client sends and serves it back as the change feed.
class FakeTrackingServer {
  final records = <String, Map<String, dynamic>>{};
  var seq = 0;
  var feed = <Map<String, dynamic>>[];
  var reject = false;

  ResponseBody handle(RequestOptions r) {
    final path = r.uri.path;
    if (r.method == 'GET' && path == '/v1/sync') {
      final since = int.parse(r.uri.queryParameters['since']!);
      final changes = [
        ...records.values.where((t) => (t['changeSeq'] as int) > since),
        ...feed,
      ];
      feed = [];
      return FakeApi.json(200, {
        'topicTests': changes,
        'cursor': seq,
        'hasMore': false,
      });
    }
    final id = path.split('/').last;
    if (r.method == 'PUT') {
      if (reject) {
        return FakeApi.json(400, {
          'status': 400,
          'title': 'Doğrulama hatası',
          'errors': {
            'results[0].topicId': ['Konu bulunamadı.'],
          },
        });
      }
      final body = Map<String, dynamic>.from(r.data as Map);
      return FakeApi.json(
        200,
        records[id] = {
          ...body,
          'id': id,
          'deleted': false,
          'changeSeq': ++seq,
          'results': [
            for (final x in body['results'] as List<dynamic>)
              {...(x as Map<String, dynamic>), 'net': 0},
          ],
        },
      );
    }
    if (r.method == 'DELETE') {
      records[id] = {
        'id': id,
        'deleted': true,
        'changeSeq': ++seq,
        'results': <Object>[],
      };
      return FakeApi.json(204, null);
    }
    throw StateError('Unexpected ${r.method} $path');
  }
}

/// The first topic row of a stored test record.
Map<String, dynamic> firstResult(Map<String, dynamic> record) =>
    (record['results'] as List<dynamic>).first as Map<String, dynamic>;
