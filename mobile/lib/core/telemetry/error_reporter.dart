/// Port for error tracking (ADR-0022 §5). Bound in `main.dart`; features never call the SDK directly.
abstract interface class ErrorReporter {
  /// Sends a caught error. Uncaught Flutter/platform errors are reported by the SDK on its own.
  Future<void> captureError(Object error, StackTrace? stackTrace);

  /// Adds a breadcrumb under `nirengi.<category>`; only those reach Sentry. [message] must hold no
  /// personal data (it is masked as defence in depth, not as the main guard).
  Future<void> breadcrumb(String category, String message);
}

/// Default when telemetry is off (no DSN, tests): reports nothing.
class NoErrorReporter implements ErrorReporter {
  const NoErrorReporter();

  @override
  Future<void> captureError(Object error, StackTrace? stackTrace) async {}

  @override
  Future<void> breadcrumb(String category, String message) async {}
}
