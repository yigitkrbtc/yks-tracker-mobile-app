import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/design/design.dart';
import 'package:nirengi/core/design/theme/app_theme.dart';
import 'package:shadcn_ui/shadcn_ui.dart';

Widget _host(Widget child, {Brightness brightness = Brightness.light}) {
  return ShadApp.custom(
    theme: AppTheme.light(),
    darkTheme: AppTheme.dark(),
    themeMode: brightness == Brightness.dark ? ThemeMode.dark : ThemeMode.light,
    appBuilder: (context) => MaterialApp(
      theme: Theme.of(context),
      builder: (context, c) => ShadAppBuilder(child: c),
      home: Scaffold(body: child),
    ),
  );
}

void main() {
  testWidgets('NButton fires onPressed for every variant', (tester) async {
    var taps = 0;
    await tester.pumpWidget(
      _host(
        Column(
          children: [
            for (final v in NButtonVariant.values)
              NButton(label: v.name, variant: v, onPressed: () => taps++),
          ],
        ),
      ),
    );

    for (final v in NButtonVariant.values) {
      await tester.tap(find.text(v.name));
    }
    expect(taps, NButtonVariant.values.length);
  });

  testWidgets('disabled NButton ignores taps', (tester) async {
    await tester.pumpWidget(
      _host(const NButton(label: 'Kaydet', onPressed: null)),
    );
    await tester.tap(find.text('Kaydet'), warnIfMissed: false);
    expect(tester.takeException(), isNull);
  });

  testWidgets('NBadge success tone uses the success token', (tester) async {
    await tester.pumpWidget(
      _host(const NBadge('iyi', tone: NBadgeTone.success)),
    );
    final badge = tester.widget<ShadBadge>(find.byType(ShadBadge));
    expect(badge.backgroundColor, AppColors.light.success);
  });

  testWidgets('dark theme maps tokens to the Material theme', (tester) async {
    late ThemeData theme;
    await tester.pumpWidget(
      _host(
        Builder(
          builder: (context) {
            theme = Theme.of(context);
            return const NCard(title: 'Türev', child: Text('42 soru'));
          },
        ),
        brightness: Brightness.dark,
      ),
    );
    expect(theme.colorScheme.primary, AppColors.dark.primary);
    expect(theme.scaffoldBackgroundColor, AppColors.dark.background);
    expect(find.text('Türev'), findsOneWidget);
    expect(find.text('42 soru'), findsOneWidget);
  });
}
