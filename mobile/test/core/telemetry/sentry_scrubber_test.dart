import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/telemetry/sentry_scrubber.dart';
import 'package:sentry_flutter/sentry_flutter.dart';

/// ADR-0022 §5 katman 3: kişisel veriyle doldurulmuş bir olay süzgeçten geçer; serileştirilmiş JSON'da
/// hiçbiri kalmaz, izin listesindeki alanlar kalır.
void main() {
  const email = 'ali.veli@example.com';
  const ipv4 = '203.0.113.42';
  const ipv6 = '2001:db8::8a2e:370:7334';
  const jwt = 'eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.c2lnbmF0dXJlLXZhbHVl';
  const digits = '12345678';
  const deviceName = 'Yiğit Telefonu';
  const secrets = [
    email,
    ipv4,
    ipv6,
    jwt,
    digits,
    deviceName,
    'Bearer secret-token',
    'gizli-not-govdesi',
    'q=arama',
    'user-42',
    'cerez=1',
    'ekstra-deger',
    'uygulama-hash',
    'sunucu-adi',
  ];

  SentryEvent pollutedEvent() => SentryEvent(
    release: 'nirengi-mobile@1.2.3+4',
    environment: 'production',
    logger: 'nirengi.sync',
    level: SentryLevel.error,
    serverName: 'sunucu-adi',
    message: SentryMessage('Kullanıcı $email $ipv4 için hata'),
    exceptions: [
      SentryException(
        type: 'StateError',
        value: 'Bozuk $jwt kodu $digits $ipv6',
        stackTrace: SentryStackTrace(
          frames: [
            SentryStackFrame(
              module: 'nirengi.sync',
              function: 'push',
              fileName: 'sync_engine.dart',
              lineNo: 42,
              vars: const {'password': 'hunter2-$email'},
              contextLine: 'final token = "$jwt";',
            ),
          ],
        ),
      ),
    ],
    tags: const {
      'route': '/topics/:id',
      'method': 'POST',
      'status': '500',
      'email': email,
    },
    // ignore: deprecated_member_use
    extra: const {'not': 'ekstra-deger'},
    user: SentryUser(id: 'user-42', email: email, ipAddress: ipv4),
    request: SentryRequest(
      url: 'https://api.example.com/v1/x?q=arama',
      method: 'POST',
      queryString: 'q=arama',
      cookies: 'cerez=1',
      data: const {'note': 'gizli-not-govdesi'},
      headers: const {'Authorization': 'Bearer secret-token'},
    ),
    contexts: Contexts(
      device: SentryDevice(name: deviceName, model: 'Pixel'),
      operatingSystem: SentryOperatingSystem(name: 'Android', version: '15'),
      runtimes: [SentryRuntime(name: 'Dart', version: '3.13.7')],
      app: SentryApp(
        name: 'Nirengi',
        version: '1.2.3',
        deviceAppHash: 'uygulama-hash',
      ),
    )..['custom'] = {'email': email},
    breadcrumbs: [
      Breadcrumb(category: 'nirengi.sync', message: 'push $email'),
      Breadcrumb(category: 'http', message: 'GET /v1/x?q=arama'),
    ],
  );

  test('kişisel veri serileştirilmiş olayda kalmaz', () {
    final scrubbed = scrubEvent(pollutedEvent())!;
    final json = jsonEncode(scrubbed.toJson());

    for (final secret in secrets) {
      expect(json, isNot(contains(secret)), reason: secret);
    }
  });

  test('ağdan/JSON\'dan okunan bilinmeyen alanlar da kalmaz', () {
    final event = SentryEvent.fromJson({
      'event_id': '0123456789abcdef0123456789abcdef',
      'timestamp': '2026-01-01T00:00:00Z',
      'gizli_alan': email,
      'exception': {
        'values': [
          {'type': 'StateError', 'value': 'x', 'gizli': ipv4},
        ],
      },
      'breadcrumbs': [
        {
          'timestamp': '2026-01-01T00:00:00Z',
          'category': 'nirengi.sync',
          'gizli': jwt,
        },
      ],
    });

    final json = jsonEncode(scrubEvent(event)!.toJson());

    for (final secret in [email, ipv4, jwt]) {
      expect(json, isNot(contains(secret)), reason: secret);
    }
    expect(json, contains('StateError'));
    expect(json, contains('nirengi.sync'));
  });

  test('olay kimliği, zamanı ve atılan nesne korunur', () {
    final error = StateError('x');
    final original = SentryEvent(throwable: error, platform: 'other');

    final e = scrubEvent(original)!;

    expect(e.eventId, original.eventId);
    expect(e.timestamp, original.timestamp);
    expect(e.throwable, same(error));
    expect(e.platform, 'other');
  });

  test('izin listesindeki alanlar kalır, mesajlar maskelenir', () {
    final e = scrubEvent(pollutedEvent())!;

    expect(e.release, 'nirengi-mobile@1.2.3+4');
    expect(e.environment, 'production');
    expect(e.logger, 'nirengi.sync');
    expect(e.level, SentryLevel.error);
    expect(e.tags, {'route': '/topics/:id', 'method': 'POST', 'status': '500'});
    expect(e.message!.formatted, 'Kullanıcı [redacted] [redacted] için hata');

    final ex = e.exceptions!.single;
    expect(ex.type, 'StateError');
    expect(ex.value, 'Bozuk [redacted] kodu [redacted] [redacted]');
    final frame = ex.stackTrace!.frames.single;
    expect(
      [frame.module, frame.function, frame.fileName, frame.lineNo],
      ['nirengi.sync', 'push', 'sync_engine.dart', 42],
    );

    expect(e.contexts.operatingSystem?.name, 'Android');
    expect(e.contexts.runtimes.single.name, 'Dart');
    expect(e.contexts.app?.version, '1.2.3');
    expect(e.contexts.device, isNull);
    expect(e.contexts.containsKey('custom'), isFalse);

    expect(e.breadcrumbs!.map((b) => b.category), ['nirengi.sync']);
    expect(e.breadcrumbs!.single.message, 'push [redacted]');
  });

  group('scrubBreadcrumb', () {
    test(
      'yalnız nirengi. önekli kırıntılar kalır, maskelenir, verisi atılır',
      () {
        final kept = scrubBreadcrumb(
          Breadcrumb(
            category: 'nirengi.auth',
            message: 'giriş $email',
            data: const {'email': email},
          ),
        )!;
        expect(kept.message, 'giriş [redacted]');
        expect(kept.data, isNull);

        expect(scrubBreadcrumb(Breadcrumb(category: 'navigation')), isNull);
        expect(scrubBreadcrumb(Breadcrumb(category: 'http')), isNull);
        expect(scrubBreadcrumb(Breadcrumb(message: 'print')), isNull);
        expect(scrubBreadcrumb(null), isNull);
      },
    );
  });

  group('maskPii', () {
    test('kısa sayılar ve olağan metin dokunulmaz kalır', () {
      expect(maskPii('soru 1234567 ve 3.14'), 'soru 1234567 ve 3.14');
    });

    test('saat ve dosya:satır:sütun IPv6 sanılmaz', () {
      expect(maskPii('12:30:45 sync.dart:42:7'), '12:30:45 sync.dart:42:7');
    });

    test('tam ve sıkıştırılmış IPv6 maskelenir', () {
      expect(
        maskPii('a 2001:0db8:0000:0000:0000:ff00:0042:8329 b'),
        'a [redacted] b',
      );
      expect(maskPii('a fe80::1 b ::1'), 'a [redacted] b [redacted]');
    });
  });
}
