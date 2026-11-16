import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/app/app.dart';
import 'package:nirengi/core/config/app_config.dart';
import 'package:nirengi/core/network/api_client.dart';
import 'package:nirengi/core/network/client_version.dart';

import '../support/app_fakes.dart';
import '../support/auth_fakes.dart';
import '../support/shell.dart';

/// Answers every request with [status] and remembers the version header it saw.
class _Server implements HttpClientAdapter {
  _Server(this.status);

  final int status;
  final versions = <String?>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<List<int>>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    versions.add(options.headers[clientVersionHeader] as String?);
    return ResponseBody.fromString(
      jsonEncode({'type': 'client.upgrade_required', 'status': status}),
      status,
      headers: {
        Headers.contentTypeHeader: ['application/problem+json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  ProviderContainer containerWith(String? version) {
    final container = ProviderContainer(
      overrides: [clientVersionProvider.overrideWithValue(version)],
    );
    addTearDown(container.dispose);
    return container;
  }

  test('every request carries the app version', () async {
    final container = containerWith('1.4.2');
    final server = _Server(200);
    final dio = container.read(publicApiProvider)..httpClientAdapter = server;

    await dio.get<dynamic>('/v1/anything');

    expect(server.versions, ['1.4.2']);
    expect(container.read(updateRequiredProvider), isFalse);
  });

  test('without a known version no header is sent', () async {
    final container = containerWith(null);
    final server = _Server(200);
    final dio = container.read(publicApiProvider)..httpClientAdapter = server;

    await dio.get<dynamic>('/v1/anything');

    expect(server.versions, [null]);
  });

  test('a 426 marks the app as needing an update', () async {
    final container = containerWith('0.9.0');
    final dio = container.read(publicApiProvider)
      ..httpClientAdapter = _Server(426);

    await expectLater(
      dio.get<dynamic>('/v1/anything'),
      throwsA(isA<DioException>()),
    );

    expect(container.read(updateRequiredProvider), isTrue);
  });

  test('other errors do not', () async {
    final container = containerWith('1.0.0');
    final dio = container.read(publicApiProvider)
      ..httpClientAdapter = _Server(500);

    await expectLater(
      dio.get<dynamic>('/v1/anything'),
      throwsA(isA<DioException>()),
    );

    expect(container.read(updateRequiredProvider), isFalse);
  });

  testWidgets('the app shows the update screen once the API answers 426', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: fakeAppOverrides(
          store: MemorySessionStore(refreshToken: 'refresh-old'),
        ),
        child: const NirengiApp(),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Merhaba Ada'), findsOneWidget);

    final container = ProviderScope.containerOf(
      tester.element(find.byType(MaterialApp)),
    );
    container.read(updateRequiredProvider.notifier).markRequired();
    await tester.pumpAndSettle();

    expect(find.text('Güncelleme gerekli'), findsOneWidget);
    expect(find.text('Merhaba Ada'), findsNothing);
    await disposeShell(tester);
  });

  group('release builds accept only an https API address', () {
    test('http is refused in release', () {
      expect(
        () => AppConfig.ensureSecureApi(
          url: 'http://192.168.1.20:8080',
          release: true,
        ),
        throwsStateError,
      );
    });

    test('a scheme-less or malformed address is refused in release', () {
      expect(
        () => AppConfig.ensureSecureApi(url: 'api.example.com', release: true),
        throwsStateError,
      );
      expect(
        () => AppConfig.ensureSecureApi(url: '', release: true),
        throwsStateError,
      );
    });

    test('https passes in release', () {
      AppConfig.ensureSecureApi(url: 'https://api.example.com', release: true);
    });

    test('http passes in debug and profile', () {
      AppConfig.ensureSecureApi(url: 'http://10.0.2.2:8080', release: false);
    });
  });
}
