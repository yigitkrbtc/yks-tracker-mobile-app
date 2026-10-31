import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/database/cache_store.dart';
import 'package:nirengi/features/profile/application/profile_providers.dart';
import 'package:nirengi/features/profile/data/drift_profile_cache.dart';
import 'package:nirengi/features/profile/domain/profile_repository.dart';
import 'package:nirengi/features/profile/domain/study_profile.dart';

import '../../support/app_fakes.dart';
import '../../support/auth_fakes.dart';
import '../../support/sync_fakes.dart';

class OfflineProfileRepository implements ProfileRepository {
  @override
  Future<StudyProfile?> get() async =>
      throw const ProfileException('offline', isNetworkError: true);

  @override
  Future<StudyProfile> save(StudyProfile profile) => get().then((p) => p!);
}

void main() {
  ProviderContainer containerWith(
    ProfileRepository repository,
    ProfileCache cache,
  ) {
    final container = ProviderContainer(
      overrides: [
        ...fakeAuthOverrides(
          FakeAuthRepository(),
          MemorySessionStore(refreshToken: 'refresh-old'),
        ),
        profileRepositoryProvider.overrideWithValue(repository),
        profileCacheProvider.overrideWithValue(cache),
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  test('a loaded profile is kept and used when offline', () async {
    final db = memoryDatabase();
    addTearDown(db.close);
    final cache = DriftProfileCache(CacheStore(db));

    final online = containerWith(FakeProfileRepository(sayProfile), cache);
    expect(await online.read(studyProfileProvider.future), isNotNull);

    final offline = containerWith(OfflineProfileRepository(), cache);
    final profile = await offline.read(studyProfileProvider.future);
    expect(profile?.primaryScoreType, ScoreType.say);
  });

  test('offline with nothing cached is an error, not onboarding', () async {
    final db = memoryDatabase();
    addTearDown(db.close);
    final container = containerWith(
      OfflineProfileRepository(),
      DriftProfileCache(CacheStore(db)),
    );

    container.listen(studyProfileProvider, (_, _) {});
    await Future<void>.delayed(const Duration(milliseconds: 20));

    // Riverpod retries failed providers; the state carries the error meanwhile.
    expect(container.read(studyProfileProvider).error, isA<ProfileException>());
  });
}
