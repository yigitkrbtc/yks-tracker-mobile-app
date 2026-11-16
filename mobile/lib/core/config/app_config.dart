import 'package:flutter/foundation.dart';

/// Build-time configuration. Values come from `--dart-define`, so no secret lives in the binary.
abstract final class AppConfig {
  /// Working product name; the final brand is decided later.
  static const appName = 'Nirengi';

  static const apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:8080',
  );

  /// A release build talks to the API over HTTPS only (ADR-0021 İ8). Call at start-up, before the first request.
  /// Debug and profile builds may use plain HTTP (the developer's PC on the local network).
  static void ensureSecureApi({
    String url = apiBaseUrl,
    bool release = kReleaseMode,
  }) {
    if (release && Uri.tryParse(url)?.scheme != 'https') {
      throw StateError(
        'Release builds need an https:// API address; got "$url". '
        'Pass --dart-define=API_BASE_URL=https://...',
      );
    }
  }
}
