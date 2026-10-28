import 'package:flutter_riverpod/misc.dart';

import '../core/network/api_client.dart';
import '../core/network/session_tokens.dart';
import '../features/auth/application/auth_providers.dart';
import '../features/auth/data/http_auth_repository.dart';
import '../features/auth/data/secure_session_store.dart';
import '../features/curriculum/application/curriculum_providers.dart';
import '../features/curriculum/data/http_curriculum_repository.dart';
import '../features/profile/application/profile_providers.dart';
import '../features/profile/data/http_profile_repository.dart';

/// Binds feature ports to their real implementations; tests pass their own overrides instead.
final List<Override> appOverrides = [
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
