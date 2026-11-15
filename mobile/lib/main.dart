import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:package_info_plus/package_info_plus.dart';

import 'app/app.dart';
import 'app/composition.dart';
import 'core/config/app_config.dart';
import 'core/network/client_version.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  AppConfig.ensureSecureApi();
  final info = await PackageInfo.fromPlatform();
  runApp(
    ProviderScope(
      overrides: [
        ...appOverrides,
        clientVersionProvider.overrideWithValue(info.version),
      ],
      child: const NirengiApp(),
    ),
  );
}
