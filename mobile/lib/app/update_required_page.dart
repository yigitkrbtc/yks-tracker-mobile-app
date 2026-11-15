import 'package:flutter/material.dart';

import '../core/config/app_config.dart';
import '../core/design/design.dart';

/// Shown when the API refuses this app version (426, ADR-0021 M4). There is nothing to retry: the person
/// has to install a newer version.
class UpdateRequiredPage extends StatelessWidget {
  const UpdateRequiredPage({super.key});

  static const path = '/update-required';

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: NCard(
              title: 'Güncelleme gerekli',
              description:
                  '${AppConfig.appName} uygulamasının bu sürümü artık desteklenmiyor. '
                  'Devam etmek için lütfen uygulamanın son sürümünü yükle. '
                  'Kayıtların cihazında güvende, güncellemeden sonra kaldığın yerden devam edersin.',
            ),
          ),
        ),
      ),
    );
  }
}
