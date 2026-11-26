import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'error_reporter.dart';

/// Bound to Sentry in `main.dart` when a DSN is given; reports nothing by default.
final errorReporterProvider = Provider<ErrorReporter>(
  (ref) => const NoErrorReporter(),
);
