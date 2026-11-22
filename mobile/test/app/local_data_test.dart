import 'dart:async';

import 'package:drift/drift.dart' show driftRuntimeOptions;
import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/app/app.dart';
import 'package:nirengi/app/local_data.dart';
import 'package:nirengi/core/database/app_database.dart';
import 'package:nirengi/core/database/cache_store.dart';
import 'package:nirengi/core/database/database_providers.dart';
import 'package:nirengi/core/sync/outbox_store.dart';
import 'package:nirengi/core/sync/sync_providers.dart';

import '../support/shell.dart';
import '../support/app_fakes.dart';
import '../support/auth_fakes.dart';
import '../support/sync_fakes.dart';

/// A database whose wipe waits for [gate], to look at the app meanwhile.
class _GatedDatabase extends AppDatabase {
  _GatedDatabase() : super(NativeDatabase.memory());

  final gate = Completer<void>();
  bool failWipe = false;

  @override
  Future<void> wipe() async {
    if (failWipe) {
      throw StateError('disk');
    }
    await gate.future;
    await super.wipe();
  }
}

void main() {
  final api = FakeApi();

  setUp(() => api.requests.clear());

  /// A device database that [owner] used last, with a leftover row and a queued write.
  Future<_GatedDatabase> seed(WidgetTester t, String owner) async {
    driftRuntimeOptions.dontWarnAboutMultipleDatabases = true;
    final db = _GatedDatabase();
    final cache = CacheStore(db);
    final outbox = OutboxStore(db);
    await t.runAsync(() async {
      await cache.write(LocalData.ownerKey, {'userId': owner});
      await cache.write('leftover', {'n': 1});
      await outbox.enqueue(
        entityType: 'thing',
        entityId: 'a',
        operation: 'put',
      );
    });
    outbox.dispose();
    return db;
  }

  Future<ProviderContainer> pumpOn(WidgetTester t, AppDatabase db) async {
    final store = MemorySessionStore(refreshToken: 'refresh-old');
    await t.pumpWidget(
      ProviderScope(
        overrides: fakeAppOverrides(store: store, database: db, api: api),
        child: const NirengiApp(),
      ),
    );
    return ProviderScope.containerOf(t.element(find.byType(NirengiApp)));
  }

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
      await disposeShell(tester);
    },
  );

  testWidgets('old rows are wiped before the shell shows', (tester) async {
    final db = await seed(tester, 'someone-else');
    final cache = CacheStore(db);
    final container = await pumpOn(tester, db);
    for (var i = 0; i < 10; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }

    expect(find.text('Merhaba Ada'), findsNothing);
    expect(find.byType(NavigationBar), findsNothing);
    expect(container.read(localDataReadyProvider), isFalse);
    expect(container.read(syncSchedulerProvider).isRunning, isFalse);
    expect(api.calls.where((c) => c.contains('/v1/sync')), isEmpty);
    final before = await tester.runAsync(() => cache.read('leftover'));
    expect(before, isNotNull, reason: 'the wipe has not run yet');

    db.gate.complete();
    await tester.pumpAndSettle();

    expect(find.text('Merhaba Ada'), findsOneWidget);
    expect(container.read(localDataReadyProvider), isTrue);
    expect(container.read(syncSchedulerProvider).isRunning, isTrue);
    final after = await tester.runAsync(() => cache.read('leftover'));
    expect(after, isNull);
    final outbox = container.read(outboxStoreProvider);
    expect(await tester.runAsync(outbox.queuedCount), 0);
    final owner = await tester.runAsync(() => cache.read(LocalData.ownerKey));
    expect(owner?['userId'], testProfile.id);
    await disposeShell(tester);
  });

  testWidgets('a failed wipe signs out and shows no old rows', (tester) async {
    final db = await seed(tester, 'someone-else');
    db.failWipe = true;
    final container = await pumpOn(tester, db);
    await tester.pumpAndSettle();

    expect(tester.takeException(), isA<StateError>());
    expect(find.widgetWithText(FilledButton, 'Giriş yap'), findsOneWidget);
    expect(find.text('Merhaba Ada'), findsNothing);
    expect(container.read(localDataReadyProvider), isFalse);
    await disposeShell(tester);
  });

  testWidgets('the same student keeps data and queued writes', (tester) async {
    final db = await seed(tester, testProfile.id);
    final cache = CacheStore(db);
    final container = await pumpOn(tester, db);
    await tester.pumpAndSettle();

    expect(find.text('Merhaba Ada'), findsOneWidget);
    final kept = await tester.runAsync(() => cache.read('leftover'));
    expect(kept, isNotNull);
    final outbox = container.read(outboxStoreProvider);
    expect(await tester.runAsync(outbox.queuedCount), 1);
    await disposeShell(tester);
  });
}
