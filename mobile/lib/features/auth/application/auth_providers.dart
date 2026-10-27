import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/auth_repository.dart';
import 'session_token_manager.dart';

/// Bound to the HTTP and secure-storage implementations in `app/composition.dart`; tests bind fakes.
final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

final sessionStoreProvider = Provider<SessionStore>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

final sessionTokenManagerProvider = Provider<SessionTokenManager>(
  (ref) => SessionTokenManager(
    ref.watch(authRepositoryProvider),
    ref.watch(sessionStoreProvider),
  ),
);
