import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../database/database_providers.dart';
import '../network/api_client.dart';
import 'outbox_store.dart';
import 'sync_handler.dart';
import 'sync_scheduler.dart';
import 'sync_service.dart';

/// Features' sync handlers, listed in `app/composition.dart`.
final syncHandlersProvider = Provider<List<SyncHandler>>((ref) => const []);

final outboxStoreProvider = Provider<OutboxStore>((ref) {
  final store = OutboxStore(ref.watch(appDatabaseProvider));
  ref.onDispose(store.dispose);
  return store;
});

final syncServiceProvider = Provider<SyncService>((ref) {
  final service = SyncService(
    ref.watch(appDatabaseProvider),
    ref.watch(outboxStoreProvider),
    ref.watch(apiClientProvider),
    ref.watch(syncHandlersProvider),
  );
  ref.onDispose(service.dispose);
  return service;
});

final syncSchedulerProvider = Provider<SyncScheduler>((ref) {
  final scheduler = SyncScheduler(
    ref.watch(syncServiceProvider),
    ref.watch(outboxStoreProvider),
  );
  ref.onDispose(scheduler.stop);
  return scheduler;
});

/// Writes not yet on the server (refused ones included); shown before sign-out.
final queuedWriteCountProvider = StreamProvider<int>(
  (ref) => ref.watch(outboxStoreProvider).watchQueuedCount(),
);
