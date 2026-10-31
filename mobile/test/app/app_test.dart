import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/app/app.dart';

import '../support/app_fakes.dart';
import '../support/auth_fakes.dart';

void main() {
  Future<void> pumpApp(
    WidgetTester tester,
    MemorySessionStore store, {
    FakeProfileRepository? profiles,
  }) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: fakeAppOverrides(store: store, profiles: profiles),
        child: const NirengiApp(),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('signed-out start lands on login', (tester) async {
    await pumpApp(tester, MemorySessionStore());

    expect(find.widgetWithText(FilledButton, 'Giriş yap'), findsOneWidget);
  });

  testWidgets('stored session opens the topics of the student\'s score type', (
    tester,
  ) async {
    await pumpApp(tester, MemorySessionStore(refreshToken: 'refresh-old'));

    expect(find.text('Merhaba Ada'), findsOneWidget);
    expect(find.byType(NavigationDestination), findsNWidgets(4));
    expect(find.widgetWithText(Tab, 'AYT'), findsOneWidget);
    expect(find.text('Türkçe'), findsOneWidget);

    await tester.tap(find.widgetWithText(Tab, 'AYT'));
    await tester.pumpAndSettle();
    expect(find.text('Fizik'), findsOneWidget);
    expect(find.text('Türk Dili ve Edebiyatı'), findsNothing);

    await tester.tap(find.text('Fizik'));
    await tester.pumpAndSettle();
    expect(find.text('Fizik konu 1'), findsOneWidget);
    expect(find.text('Çok çıkar'), findsOneWidget);
    expect(find.text('Son yıllarda çıkmadı'), findsOneWidget);

    await tester.tap(find.text('Analiz'));
    await tester.pumpAndSettle();
    expect(find.text('Analiz yakında'), findsOneWidget);
  });

  testWidgets('first sign-in goes through onboarding, then shows topics', (
    tester,
  ) async {
    final profiles = FakeProfileRepository();
    await pumpApp(
      tester,
      MemorySessionStore(refreshToken: 'refresh-old'),
      profiles: profiles,
    );

    expect(find.text('Hoş geldin'), findsOneWidget);

    await tester.tap(find.widgetWithText(ChoiceChip, '12. sınıf'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ChoiceChip, 'Sözel').first);
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Başla'));
    await tester.tap(find.text('Başla'));
    await tester.pumpAndSettle();

    expect(profiles.saved, hasLength(1));
    expect(find.text('Merhaba Ada'), findsOneWidget);
    await tester.tap(find.widgetWithText(Tab, 'AYT'));
    await tester.pumpAndSettle();
    expect(find.text('Felsefe Grubu'), findsOneWidget);
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
    expect(find.text('Türkçe'), findsOneWidget);
  });

  testWidgets('search opens the sections that hold matching topics', (
    tester,
  ) async {
    await pumpApp(tester, MemorySessionStore(refreshToken: 'refresh-old'));
    expect(find.text('Türkçe konu 1'), findsNothing);

    await tester.enterText(find.byType(TextField), 'türkçe konu');
    await tester.pumpAndSettle();

    expect(find.text('Türkçe konu 1'), findsOneWidget);
  });

  testWidgets('signing in again with a profile skips onboarding', (
    tester,
  ) async {
    await pumpApp(tester, MemorySessionStore(refreshToken: 'refresh-old'));
    await tester.tap(find.byIcon(Icons.account_circle_outlined));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Çıkış yap'));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'E-posta'),
      'ada@example.com',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Şifre'),
      'dogruSifre123',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Giriş yap'));
    await tester.pump();
    expect(find.text('Hoş geldin'), findsNothing);
    await tester.pumpAndSettle();

    expect(find.text('Hoş geldin'), findsNothing);
    expect(find.text('Merhaba Ada'), findsOneWidget);
  });
}
