import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/database/cache_store.dart';
import 'package:nirengi/features/curriculum/application/curriculum_providers.dart';
import 'package:nirengi/features/curriculum/data/drift_curriculum_cache.dart';
import 'package:nirengi/features/curriculum/domain/curriculum.dart';
import 'package:nirengi/features/curriculum/domain/curriculum_repository.dart';

import '../../support/app_fakes.dart';
import '../../support/sync_fakes.dart';

class OfflineCurriculumRepository implements CurriculumRepository {
  @override
  Future<Curriculum?> fetch({String? ifNoneMatch}) async =>
      throw const CurriculumException('offline');
}

void main() {
  test(
    'curriculum round-trips through the device cache, sources included',
    () async {
      final db = memoryDatabase();
      addTearDown(db.close);
      final cache = DriftCurriculumCache(CacheStore(db));
      final original = Curriculum.fromJson({
        ...seedCurriculum().toJson(),
        'sources': [
          {
            'id': 's1',
            'code': 'OSYM',
            'name': 'ÖSYM çıkmış sorular',
            'kind': 'Official',
          },
        ],
      });

      await cache.write(original);
      final read = (await cache.read())!;

      expect(read.toJson(), original.toJson());
      expect(read.sources.single.kind, SourceKind.official);
      expect(read.topicsById.length, original.subjects.length * 2);
    },
  );

  test('offline start shows the cached curriculum', () async {
    final db = memoryDatabase();
    addTearDown(db.close);
    final cache = DriftCurriculumCache(CacheStore(db));
    await cache.write(seedCurriculum());
    final container = ProviderContainer(
      overrides: [
        curriculumRepositoryProvider.overrideWithValue(
          OfflineCurriculumRepository(),
        ),
        curriculumCacheProvider.overrideWithValue(cache),
      ],
    );
    addTearDown(container.dispose);

    final curriculum = await container.read(curriculumProvider.future);
    await Future<void>.delayed(
      Duration.zero,
    ); // the failed revalidation runs and keeps the copy

    expect(curriculum.version, 'v1');
    expect(container.read(curriculumProvider).value?.version, 'v1');
  });

  test('a fresh download replaces the cached copy', () async {
    final db = memoryDatabase();
    addTearDown(db.close);
    final cache = DriftCurriculumCache(CacheStore(db));
    await cache.write(seedCurriculum());
    final server = FakeCurriculumRepository(
      Curriculum.fromJson({...seedCurriculum().toJson(), 'version': 'v2'}),
    );
    final container = ProviderContainer(
      overrides: [
        curriculumRepositoryProvider.overrideWithValue(server),
        curriculumCacheProvider.overrideWithValue(cache),
      ],
    );
    addTearDown(container.dispose);

    await container.read(curriculumProvider.future);
    await Future<void>.delayed(const Duration(milliseconds: 10));

    expect(server.requests, ['v1']);
    expect(container.read(curriculumProvider).value?.version, 'v2');
    expect((await cache.read())!.version, 'v2');
  });
}
