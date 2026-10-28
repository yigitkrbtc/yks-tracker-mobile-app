import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/auth/application/session_controller.dart';
import 'package:nirengi/features/auth/domain/auth_failure.dart';
import 'package:nirengi/features/auth/domain/auth_models.dart';

import '../../support/auth_fakes.dart';

void main() {
  late FakeAuthRepository repository;

  setUp(() => repository = FakeAuthRepository());

  ProviderContainer containerWith(MemorySessionStore store) {
    final container = ProviderContainer(
      overrides: fakeAuthOverrides(repository, store),
    );
    addTearDown(container.dispose);
    return container;
  }

  test('no stored token means signed out', () async {
    final container = containerWith(MemorySessionStore());

    expect(
      await container.read(sessionControllerProvider.future),
      isA<SignedOutState>(),
    );
  });

  test('a stored token restores the session', () async {
    final container = containerWith(
      MemorySessionStore(refreshToken: 'refresh-old'),
    );

    final state = await container.read(sessionControllerProvider.future);

    expect(state, isA<SignedInState>());
    expect(repository.calls, ['refresh:refresh-old', 'me:access-r1']);
  });

  test('offline start keeps the last known profile', () async {
    repository.refreshError = networkError;
    final container = containerWith(
      MemorySessionStore(refreshToken: 'refresh-old', profile: testProfile),
    );

    final state = await container.read(sessionControllerProvider.future);

    expect((state as SignedInState).profile.firstName, 'Ada');
  });

  test('a transient server failure at start keeps the session', () async {
    repository.refreshError = const AuthException(AuthFailure.tooManyRequests);
    final store = MemorySessionStore(
      refreshToken: 'refresh-old',
      profile: testProfile,
    );
    final container = containerWith(store);

    final state = await container.read(sessionControllerProvider.future);

    expect((state as SignedInState).profile.firstName, 'Ada');
    expect(store.refreshToken, 'refresh-old');
  });

  test('a revoked session at start signs out and forgets the token', () async {
    repository.refreshError = expiredSession;
    final store = MemorySessionStore(
      refreshToken: 'refresh-old',
      profile: testProfile,
    );
    final container = containerWith(store);

    expect(
      await container.read(sessionControllerProvider.future),
      isA<SignedOutState>(),
    );
    expect(store.refreshToken, isNull);
  });

  test('login signs in and persists the refresh token', () async {
    final store = MemorySessionStore();
    final container = containerWith(store);
    await container.read(sessionControllerProvider.future);

    await container
        .read(sessionControllerProvider.notifier)
        .login('ada@example.com', 'x');

    expect(
      container.read(sessionControllerProvider).value,
      isA<SignedInState>(),
    );
    expect(store.refreshToken, 'refresh-login');
  });

  test('admin accounts are turned away from the student app', () async {
    repository.loginOutcome = const SecondFactorRequired();
    final container = containerWith(MemorySessionStore());
    await container.read(sessionControllerProvider.future);

    await expectLater(
      container
          .read(sessionControllerProvider.notifier)
          .login('admin@example.com', 'x'),
      throwsA(
        isA<AuthException>().having(
          (e) => e.failure,
          'failure',
          AuthFailure.secondFactorRequired,
        ),
      ),
    );
    expect(
      container.read(sessionControllerProvider).value,
      isA<SignedOutState>(),
    );
  });

  test('logout and deletion both end the local session', () async {
    final store = MemorySessionStore(refreshToken: 'refresh-old');
    final container = containerWith(store);
    await container.read(sessionControllerProvider.future);
    final controller = container.read(sessionControllerProvider.notifier);

    final purgeAt = await controller.deleteAccount('x');

    expect(purgeAt, DateTime.utc(2026, 11, 26));
    expect(repository.calls.last, 'delete:access-r1');
    expect(
      container.read(sessionControllerProvider).value,
      isA<SignedOutState>(),
    );
    expect(store.refreshToken, isNull);

    await controller.login('ada@example.com', 'x');
    await controller.logout();
    expect(repository.calls.last, 'logout:refresh-login');
    expect(
      container.read(sessionControllerProvider).value,
      isA<SignedOutState>(),
    );
  });
}
