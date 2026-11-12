import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/auth/presentation/verify_email_page.dart';

import '../../support/auth_fakes.dart';

void main() {
  late FakeAuthRepository repository;

  Future<void> pumpVerify(WidgetTester tester, {String? password}) async {
    repository = FakeAuthRepository();
    await tester.pumpWidget(
      ProviderScope(
        overrides: fakeAuthOverrides(repository, MemorySessionStore()),
        child: MaterialApp(
          home: VerifyEmailPage(email: 'ada@example.com', password: password),
        ),
      ),
    );
  }

  testWidgets('sends the password typed at sign-up with the code', (
    tester,
  ) async {
    await pumpVerify(tester, password: 'dogruSifre123');

    expect(find.text('Kayıtta seçtiğin şifre'), findsNothing);
    await tester.enterText(find.byType(TextFormField).first, '123456');
    await tester.tap(find.text('Doğrula'));
    await tester.pump();

    expect(
      repository.calls,
      contains('verify:ada@example.com:123456:dogruSifre123'),
    );
  });

  testWidgets('asks for the password when it is not at hand', (tester) async {
    await pumpVerify(tester);

    await tester.enterText(find.byType(TextFormField).first, '123456');
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Kayıtta seçtiğin şifre'),
      'dogruSifre123',
    );
    await tester.tap(find.text('Doğrula'));
    await tester.pump();

    expect(
      repository.calls,
      contains('verify:ada@example.com:123456:dogruSifre123'),
    );
  });
}
