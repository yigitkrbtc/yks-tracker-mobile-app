/// Build-time configuration. Values come from `--dart-define`, so no secret lives in the binary.
abstract final class AppConfig {
  /// Working product name; the final brand is decided later.
  static const appName = 'Nirengi';

  static const apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:8080',
  );
}
