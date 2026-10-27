import 'package:flutter_riverpod/misc.dart';

import '../core/network/api_client.dart';
import '../core/network/session_tokens.dart';
import '../features/auth/application/auth_providers.dart';
import '../features/auth/data/http_auth_repository.dart';
import '../features/auth/data/secure_session_store.dart';

/// Binds feature ports to their real implementations; tests pass their own overrides instead.
final List<Override> appOverrides = [
  authRepositoryProvider.overrideWith(
    (ref) => HttpAuthRepository(ref.watch(publicApiProvider)),
  ),
  sessionStoreProvider.overrideWith((ref) => SecureSessionStore()),
  ...sessionOverrides,
];

/// Lets core's HTTP client use the auth feature's tokens without importing the feature.
final List<Override> sessionOverrides = [
  sessionTokensProvider.overrideWith(
    (ref) => ref.watch(sessionTokenManagerProvider),
  ),
];
