import 'package:drift_flutter/drift_flutter.dart';
import 'package:flutter_riverpod/misc.dart';

import '../core/database/app_database.dart';
import '../core/database/cache_store.dart';
import '../core/database/database_providers.dart';

import '../core/network/api_client.dart';
import '../core/network/session_tokens.dart';
import '../features/auth/application/auth_providers.dart';
import '../features/auth/data/http_auth_repository.dart';
import '../features/auth/data/secure_session_store.dart';
import '../features/curriculum/application/curriculum_providers.dart';
import '../features/curriculum/data/drift_curriculum_cache.dart';
import '../features/curriculum/data/http_curriculum_repository.dart';
import '../features/profile/application/profile_providers.dart';
import '../features/profile/data/drift_profile_cache.dart';
import '../features/profile/data/http_profile_repository.dart';

/// Binds feature ports to their real implementations; tests pass their own overrides instead.
final List<Override> appOverrides = [
  appDatabaseProvider.overrideWith((ref) {
    final db = AppDatabase(driftDatabase(name: 'nirengi'));
    ref.onDispose(db.close);
    return db;
  }),
  profileCacheProvider.overrideWith(
    (ref) => DriftProfileCache(CacheStore(ref.watch(appDatabaseProvider))),
  ),
  curriculumCacheProvider.overrideWith(
    (ref) => DriftCurriculumCache(CacheStore(ref.watch(appDatabaseProvider))),
  ),
  authRepositoryProvider.overrideWith(
    (ref) => HttpAuthRepository(ref.watch(publicApiProvider)),
  ),
  sessionStoreProvider.overrideWith((ref) => SecureSessionStore()),
  profileRepositoryProvider.overrideWith(
    (ref) => HttpProfileRepository(ref.watch(apiClientProvider)),
  ),
  curriculumRepositoryProvider.overrideWith(
    (ref) => HttpCurriculumRepository(ref.watch(apiClientProvider)),
  ),
  ...sessionOverrides,
];

/// Lets core's HTTP client use the auth feature's tokens without importing the feature.
final List<Override> sessionOverrides = [
  sessionTokensProvider.overrideWith(
    (ref) => ref.watch(sessionTokenManagerProvider),
  ),
];
