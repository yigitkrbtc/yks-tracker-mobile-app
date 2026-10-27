import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/app/app.dart';

import '../support/auth_fakes.dart';

void main() {
  Future<void> pumpApp(WidgetTester tester, MemorySessionStore store) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: fakeAuthOverrides(FakeAuthRepository(), store),
        child: const NirengiApp(),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('signed-out start lands on login', (tester) async {
    await pumpApp(tester, MemorySessionStore());

    expect(find.widgetWithText(FilledButton, 'Giriş yap'), findsOneWidget);
  });

  testWidgets('stored session opens the home shell with three tabs', (
    tester,
  ) async {
    await pumpApp(tester, MemorySessionStore(refreshToken: 'refresh-old'));

    expect(find.text('Merhaba Ada'), findsOneWidget);
    expect(find.byType(NavigationDestination), findsNWidgets(3));
    expect(find.text('Konular yakında'), findsOneWidget);

    await tester.tap(find.text('Denemeler'));
    await tester.pumpAndSettle();
    expect(find.text('Denemeler yakında'), findsOneWidget);
  });

  testWidgets('logging in from the login screen opens home', (tester) async {
    await pumpApp(tester, MemorySessionStore());

    await tester.enterText(
      find.widgetWithText(TextFormField, 'E-posta'),
      'ada@example.com',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Şifre'),
      'dogruSifre123',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Giriş yap'));
    await tester.pumpAndSettle();

    expect(find.text('Merhaba Ada'), findsOneWidget);
  });
}
