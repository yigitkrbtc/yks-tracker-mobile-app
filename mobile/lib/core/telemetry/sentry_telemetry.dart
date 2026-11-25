import 'package:flutter/foundation.dart';
import 'package:sentry_flutter/sentry_flutter.dart';

import 'error_reporter.dart';
import 'sentry_scrubber.dart';

/// Starts Sentry when [dsn] is set and returns the reporter to bind; with no DSN nothing starts
/// (local runs, preview APKs, tests). A malformed DSN or a failing SDK start leaves telemetry off
/// instead of stopping the app before its first frame.
Future<ErrorReporter> startTelemetry({
  required String dsn,
  required String environment,
  required String release,
}) async {
  if (!_isValidDsn(dsn)) return const NoErrorReporter();
  try {
    await SentryFlutter.init(
      (o) => configureSentryOptions(
        o,
        dsn: dsn,
        environment: environment,
        release: release,
      ),
    );
  } on Object catch (e) {
    debugPrint('Sentry could not start: ${e.runtimeType}');
    return const NoErrorReporter();
  }
  return Sentry.isEnabled
      ? SentryErrorReporter(HubAdapter())
      : const NoErrorReporter();
}

bool _isValidDsn(String dsn) {
  if (dsn.isEmpty) return false;
  try {
    final parsed = Dsn.parse(dsn);
    return parsed.uri?.scheme.startsWith('http') ?? false;
  } on Object {
    return false;
  }
}

/// ADR-0022 §5 katman 1. Kişisel veri, ekran, iz ve yerel SDK kapalı; olay ve kırıntı izin listesiyle
/// süzülür. `SentryNavigatorObserver` ve `SentryHttpClient` bilerek eklenmez (rota `extra`'sı ve URL'ler
/// kişisel veri taşıyabilir).
void configureSentryOptions(
  SentryFlutterOptions o, {
  required String dsn,
  required String environment,
  required String release,
}) {
  // Süzgeçler önce, DSN en son: SDK seçenek geri çağırmasındaki hatayı yalnız loglayıp devam eder;
  // arada bir atama hata verirse SDK DSN'siz (kapalı) kalır, süzgeçsiz açılmaz.
  o
    ..beforeSend = ((event, hint) => scrubEvent(event))
    ..beforeBreadcrumb = ((crumb, hint) => scrubBreadcrumb(crumb))
    ..sendDefaultPii = false
    ..maxRequestBodySize = MaxRequestBodySize.never
    ..tracesSampleRate = 0
    ..enableAutoPerformanceTracing = false
    ..enableUserInteractionTracing = false
    ..attachScreenshot = false
    ..attachViewHierarchy = false
    ..enableAutoSessionTracking = false
    ..enableUserInteractionBreadcrumbs = false
    ..enablePrintBreadcrumbs = false
    // Yerel SDK Android'de yalnız taşıma katmanıdır: Dart olayı süzülmüş zarf olarak ona verir
    // (FileSystemTransport). Kendi topladıkları kapalı: yerel çökme/ANR olayları `beforeSend`'den
    // geçmez ve kurulum kimliği taşır; Play Console Android vitals'ta görünürler.
    ..autoInitializeNativeSdk = true
    ..enableNativeCrashHandling = false
    ..anrEnabled = false
    ..enableAutoNativeBreadcrumbs = false
    ..environment = environment
    ..release = release
    ..dsn = dsn;
}

/// [ErrorReporter] adapter over a Sentry [Hub].
class SentryErrorReporter implements ErrorReporter {
  SentryErrorReporter(this._hub);

  final Hub _hub;

  @override
  Future<void> captureError(Object error, StackTrace? stackTrace) =>
      _hub.captureException(error, stackTrace: stackTrace);

  @override
  Future<void> breadcrumb(String category, String message) =>
      _hub.addBreadcrumb(
        Breadcrumb(
          category: '$breadcrumbCategoryPrefix$category',
          message: message,
        ),
      );
}
