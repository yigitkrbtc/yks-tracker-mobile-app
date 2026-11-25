import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/config/app_config.dart';
import 'package:nirengi/core/telemetry/error_reporter.dart';
import 'package:nirengi/core/telemetry/sentry_telemetry.dart';
import 'package:sentry_flutter/sentry_flutter.dart';

/// ADR-0022 §5 katman 1: SDK kişisel veri toplayan her özelliği kapalı başlar.
void main() {
  group('configureSentryOptions', () {
    late SentryFlutterOptions o;

    setUp(() {
      o = SentryFlutterOptions();
      configureSentryOptions(
        o,
        dsn: 'https://key@o1.ingest.de.sentry.io/1',
        environment: 'production',
        release: 'nirengi-mobile@1.2.3+4',
      );
    });

    test('kimlik ve sürüm bilgisi verilen değerlerdir', () {
      expect(o.dsn, 'https://key@o1.ingest.de.sentry.io/1');
      expect(o.environment, 'production');
      expect(o.release, 'nirengi-mobile@1.2.3+4');
    });

    test('kişisel veri, ekran ve iz toplama kapalı', () {
      expect(o.sendDefaultPii, isFalse);
      expect(o.maxRequestBodySize, MaxRequestBodySize.never);
      expect(o.tracesSampleRate, 0);
      expect(o.enableAutoPerformanceTracing, isFalse);
      expect(o.enableUserInteractionTracing, isFalse);
      expect(o.attachScreenshot, isFalse);
      expect(o.attachViewHierarchy, isFalse);
      expect(o.enableAutoSessionTracking, isFalse);
      expect(o.enableUserInteractionBreadcrumbs, isFalse);
      expect(o.enablePrintBreadcrumbs, isFalse);
    });

    test('yerel (Android) SDK ve çökme yakalama kapalı', () {
      expect(o.autoInitializeNativeSdk, isFalse);
      expect(o.enableNativeCrashHandling, isFalse);
      expect(o.anrEnabled, isFalse);
      expect(o.enableAutoNativeBreadcrumbs, isFalse);
    });

    test('olay ve kırıntı süzgeçleri bağlı', () async {
      final event = SentryEvent(user: SentryUser(id: 'u1'));
      final sent = await o.beforeSend!(event, Hint());
      expect(sent!.user, isNull);

      expect(o.beforeBreadcrumb!(Breadcrumb(category: 'http'), Hint()), isNull);
    });
  });

  test('DSN verilmezse telemetri kapalı', () async {
    expect(AppConfig.sentryDsn, isEmpty);
    expect(AppConfig.sentryEnvironment, 'development');

    final reporter = await startTelemetry(
      dsn: '',
      environment: 'development',
      release: 'nirengi-mobile@0.0.0+0',
    );
    expect(reporter, isA<NoErrorReporter>());
    expect(Sentry.isEnabled, isFalse);
  });

  group('SentryErrorReporter', () {
    test('kırıntı kategorisine nirengi. öneki eklenir', () async {
      final hub = _RecordingHub();
      await SentryErrorReporter(hub).breadcrumb('sync', 'push başarısız');

      expect(hub.crumbs.single.category, 'nirengi.sync');
      expect(hub.crumbs.single.message, 'push başarısız');
    });

    test('hata yığınıyla iletilir', () async {
      final hub = _RecordingHub();
      final stack = StackTrace.current;
      await SentryErrorReporter(hub).captureError(StateError('x'), stack);

      expect(hub.errors.single.$1, isA<StateError>());
      expect(hub.errors.single.$2, stack);
    });
  });
}

class _RecordingHub implements Hub {
  final crumbs = <Breadcrumb>[];
  final errors = <(Object, Object?)>[];

  @override
  Future<void> addBreadcrumb(Breadcrumb crumb, {Hint? hint}) async =>
      crumbs.add(crumb);

  @override
  Future<SentryId> captureException(
    dynamic throwable, {
    dynamic stackTrace,
    Hint? hint,
    SentryMessage? message,
    ScopeCallback? withScope,
  }) async {
    errors.add((throwable as Object, stackTrace));
    return SentryId.empty();
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
