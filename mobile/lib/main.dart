import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:package_info_plus/package_info_plus.dart';

import 'app/app.dart';
import 'app/composition.dart';
import 'core/config/app_config.dart';
import 'core/network/client_version.dart';
import 'core/telemetry/sentry_telemetry.dart';
import 'core/telemetry/telemetry_providers.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  AppConfig.ensureSecureApi();
  final info = await PackageInfo.fromPlatform();
  final errorReporter = await startTelemetry(
    dsn: AppConfig.sentryDsn,
    environment: AppConfig.sentryEnvironment,
    release: 'nirengi-mobile@${info.version}+${info.buildNumber}',
  );
  runApp(
    ProviderScope(
      overrides: [
        ...appOverrides,
        clientVersionProvider.overrideWithValue(info.version),
        errorReporterProvider.overrideWithValue(errorReporter),
      ],
      child: const NirengiApp(),
    ),
  );
}
