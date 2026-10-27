import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/auth/application/session_token_manager.dart';
import 'package:nirengi/features/auth/domain/auth_failure.dart';

import '../../support/auth_fakes.dart';

void main() {
  late FakeAuthRepository repository;
  late MemorySessionStore store;
  late SessionTokenManager manager;

  setUp(() {
    repository = FakeAuthRepository();
    store = MemorySessionStore(refreshToken: 'refresh-stored');
    manager = SessionTokenManager(repository, store);
  });

  test('concurrent refreshes share one call (a reused token would kill the session)', () async {
    final results = await Future.wait([
      manager.refreshAccessToken(),
      manager.refreshAccessToken(),
      manager.refreshAccessToken(),
    ]);

    expect(repository.refreshCount, 1);
    expect(results.toSet(), {'access-r1'});
    expect(store.refreshToken, 'refresh-r1');
  });

  test('a rejected session is cleared and reported', () async {
    repository.refreshError = expiredSession;
    var expired = false;
    manager.onSessionExpired = () => expired = true;

    expect(await manager.refreshAccessToken(), isNull);
    expect(expired, isTrue);
    expect(store.refreshToken, isNull);
  });

  test('a network failure keeps the stored session', () async {
    repository.refreshError = networkError;

    await expectLater(
      manager.refresh(),
      throwsA(
        isA<AuthException>().having(
          (e) => e.failure,
          'failure',
          AuthFailure.network,
        ),
      ),
    );
    expect(store.refreshToken, 'refresh-stored');
  });

  test(
    'a fresh access token is reused, an expiring one is refreshed',
    () async {
      await manager.adopt(testTokens('a'));
      expect(await manager.freshAccessToken(), 'access-a');
      expect(repository.refreshCount, 0);

      await manager.adopt(
        testTokens(
          'b',
          expiresAt: DateTime.now().add(const Duration(seconds: 10)),
        ),
      );
      expect(await manager.freshAccessToken(), 'access-r1');
    },
  );
}
