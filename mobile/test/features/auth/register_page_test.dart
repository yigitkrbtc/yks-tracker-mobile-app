import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:nirengi/features/auth/presentation/register_page.dart';

import '../../support/auth_fakes.dart';

void main() {
  late FakeAuthRepository repository;

  Future<void> pumpRegister(WidgetTester tester) async {
    repository = FakeAuthRepository();
    final router = GoRouter(
      initialLocation: '/register',
      routes: [
        GoRoute(
          path: '/register',
          builder: (context, state) =>
              RegisterPage(now: DateTime.utc(2026, 10, 27)),
        ),
        GoRoute(
          path: '/verify',
          builder: (context, state) =>
              Text('verify ${state.uri.queryParameters['email']}'),
        ),
        GoRoute(
          path: '/legal/:doc',
          builder: (context, state) => const Text('legal'),
        ),
      ],
    );
    await tester.pumpWidget(
      ProviderScope(
        overrides: fakeAuthOverrides(repository, MemorySessionStore()),
        child: MaterialApp.router(routerConfig: router),
      ),
    );
  }

  Future<void> fillForm(WidgetTester tester) async {
    await tester.enterText(find.widgetWithText(TextFormField, 'Ad'), 'Ada');
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Soyad'),
      'Yılmaz',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'E-posta'),
      'ada@example.com',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Şifre'),
      'dogruSifre123',
    );
  }

  Future<void> pickBirthYear(WidgetTester tester, int year) async {
    await tester.tap(find.byKey(const Key('birthYear')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('$year').last);
    await tester.pumpAndSettle();
  }

  testWidgets('youngest selectable birth year is the guaranteed-13 one', (
    tester,
  ) async {
    await pumpRegister(tester);

    await tester.tap(find.byKey(const Key('birthYear')));
    await tester.pumpAndSettle();

    expect(find.text('2012'), findsWidgets);
    expect(find.text('2013'), findsNothing);
  });

  testWidgets('consent is required before sign-up is sent', (tester) async {
    await pumpRegister(tester);
    await fillForm(tester);
    await pickBirthYear(tester, 2008);

    await tester.ensureVisible(find.text('Hesap oluştur'));
    await tester.tap(find.text('Hesap oluştur'));
    await tester.pumpAndSettle();

    expect(find.text('Devam etmek için onay gerekli.'), findsOneWidget);
    expect(repository.calls, isEmpty);
  });

  testWidgets(
    'a valid sign-up goes to the code screen; minors see the parents link',
    (tester) async {
      await pumpRegister(tester);
      await fillForm(tester);
      await pickBirthYear(tester, 2008);
      expect(find.textContaining('veliler için bilgilendirme'), findsOneWidget);

      await tester.tap(find.byKey(const Key('consent')));
      await tester.ensureVisible(find.text('Hesap oluştur'));
      await tester.tap(find.text('Hesap oluştur'));
      await tester.pumpAndSettle();

      expect(repository.calls, ['register:ada@example.com:2008']);
      expect(find.text('verify ada@example.com'), findsOneWidget);
    },
  );
}
