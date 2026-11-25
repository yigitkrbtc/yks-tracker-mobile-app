import 'package:sentry_flutter/sentry_flutter.dart';

import 'error_reporter.dart';
import 'sentry_scrubber.dart';

/// Starts Sentry when [dsn] is set and returns the reporter to bind; with no DSN nothing starts
/// (local runs, preview APKs, tests).
Future<ErrorReporter> startTelemetry({
  required String dsn,
  required String environment,
  required String release,
}) async {
  if (dsn.isEmpty) return const NoErrorReporter();
  await SentryFlutter.init(
    (o) => configureSentryOptions(
      o,
      dsn: dsn,
      environment: environment,
      release: release,
    ),
  );
  return SentryErrorReporter(HubAdapter());
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
  o
    ..dsn = dsn
    ..environment = environment
    ..release = release
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
    // Yerel SDK `user.id`'ye kurulum kimliği yazar ve Dart `beforeSend`'inden geçmez; yerel
    // çökmeler Play Console Android vitals'ta görünür.
    ..autoInitializeNativeSdk = false
    ..enableNativeCrashHandling = false
    ..anrEnabled = false
    ..enableAutoNativeBreadcrumbs = false
    ..beforeSend = ((event, hint) => scrubEvent(event))
    ..beforeBreadcrumb = ((crumb, hint) => scrubBreadcrumb(crumb));
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
