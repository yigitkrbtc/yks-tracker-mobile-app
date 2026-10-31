import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/app/app.dart';
import 'package:nirengi/core/database/database_providers.dart';
import 'package:nirengi/core/sync/sync_providers.dart';

import '../support/app_fakes.dart';
import '../support/auth_fakes.dart';

void main() {
  Future<ProviderContainer> pumpApp(WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: fakeAppOverrides(
          store: MemorySessionStore(refreshToken: 'refresh-old'),
        ),
        child: const NirengiApp(),
      ),
    );
    await tester.pumpAndSettle();
    return ProviderScope.containerOf(tester.element(find.byType(NirengiApp)));
  }

  Future<void> tapLogout(WidgetTester tester) async {
    await tester.tap(find.byIcon(Icons.account_circle_outlined));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Çıkış yap'));
    await tester.pumpAndSettle();
  }

  testWidgets(
    'signing out with unsynced writes asks first, then wipes the device',
    (tester) async {
      final container = await pumpApp(tester);
      final outbox = container.read(outboxStoreProvider);
      await tester.runAsync(
        () => outbox.enqueue(
          entityType: 'thing',
          entityId: 'a',
          operation: 'put',
        ),
      );
      container.read(syncSchedulerProvider).stop(); // keep the write queued

      await tapLogout(tester);
      expect(find.text('Eşitlenmemiş kayıtlar var'), findsOneWidget);
      await tester.tap(find.text('Vazgeç'));
      await tester.pumpAndSettle();
      expect(find.text('Merhaba Ada'), findsOneWidget);

      await tapLogout(tester);
      await tester.tap(find.text('Yine de çık'));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(FilledButton, 'Giriş yap'), findsOneWidget);
      expect(await tester.runAsync(outbox.queuedCount), 0);
      final owner = await tester.runAsync(
        () => container
            .read(appDatabaseProvider)
            .select(container.read(appDatabaseProvider).cacheEntries)
            .get(),
      );
      expect(owner, isEmpty);
    },
  );
}
