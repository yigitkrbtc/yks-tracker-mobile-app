import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/database/app_database.dart';
import 'package:nirengi/core/database/cache_store.dart';
import 'package:nirengi/core/database/database_providers.dart';
import 'package:nirengi/core/network/api_client.dart';
import 'package:nirengi/features/analytics/application/analysis_providers.dart';
import 'package:nirengi/features/analytics/data/drift_analysis_cache.dart';
import 'package:nirengi/features/analytics/data/http_analysis_repository.dart';
import 'package:nirengi/features/analytics/domain/analysis.dart';
import 'package:nirengi/features/analytics/domain/analysis_repository.dart';
import 'package:nirengi/features/profile/application/profile_providers.dart';

import '../../support/analysis_fakes.dart';
import '../../support/app_fakes.dart';
import '../../support/auth_fakes.dart';
import '../../support/sync_fakes.dart';

void main() {
  test(
    'overview parses the server shape and survives the device cache',
    () async {
      final db = memoryDatabase();
      addTearDown(db.close);
      final cache = DriftAnalysisCache(CacheStore(db));
      final overview = AnalysisOverview.fromJson(
        overviewJson(
          priorities: [
            {
              'topicId': 'TYT.TUR-1',
              'priority': 1.2,
              'mastery': 0.42,
              'expectedQuestions': 4.6,
              'gain': 1.75,
              'mockBoost': 0.3,
            },
          ],
          sections: [sectionJson('TYT-TUR', gapWarning: true)],
          topics: [topicStatusJson('TYT.TUR-1')],
          tests: [aggregateJson('TYT-TUR')],
        ),
      );

      await cache.write('u1', overview);
      final read = (await cache.read('u1'))!;

      expect(read.toJson(), overview.toJson());
      expect(read.topicsById['TYT.TUR-1']!.band, MasteryBand.weak);
      expect(read.sections.single.trend.label, TrendLabel.improving);
      expect(read.hasData, isTrue);
      expect(await cache.read('u2'), isNull);
    },
  );

  test('topic card reads the nested analysis and totals', () {
    final card = TopicAnalysis.fromJson(topicCardJson('t1'));

    expect(card.confidenceLevel, ConfidenceLevel.preliminary);
    expect(card.trend.label, TrendLabel.declining);
    expect(card.records, 3);
    expect(card.recent.first.net, 11 - 6 / 4);
    expect(card.recent[1].isRepeat, isTrue);
    expect(
      TopicAnalysis.fromJson(topicCardJson('t2', records: 0)).hasData,
      isFalse,
    );
  });

  test('http repository sends the etag and treats 304 as unchanged', () async {
    final api = FakeApi()
      ..route = (request) => request.headers['If-None-Match'] == '"e1"'
          ? FakeApi.json(304, null)
          : FakeApi.json(200, overviewJson());
    final repository = HttpAnalysisRepository(api.dio);

    final first = await repository.fetchOverview();
    final second = await repository.fetchOverview(ifNoneMatch: first!.etag);

    expect(first.etag, '"e1"');
    expect(second, isNull);
    expect(api.calls, [
      'GET /v1/analytics/overview',
      'GET /v1/analytics/overview',
    ]);
  });

  test(
    'http repository reports a server problem as AnalysisException',
    () async {
      final api = FakeApi()
        ..route = (_) => FakeApi.json(404, {
          'type': 'about:blank',
          'title': 'Not found',
          'status': 404,
          'code': 'analytics.topic_not_found',
        });

      await expectLater(
        HttpAnalysisRepository(api.dio).fetchTopic('nope'),
        throwsA(isA<AnalysisException>()),
      );
    },
  );

  group('panel controller', () {
    final me = testProfile.id;

    ProviderContainer container(
      AppDatabase db,
      FakeAnalysisRepository repository,
      AnalysisCache cache,
    ) {
      final c = ProviderContainer(
        overrides: [
          appDatabaseProvider.overrideWithValue(db),
          apiClientProvider.overrideWithValue(FakeApi().dio),
          profileRepositoryProvider.overrideWithValue(
            FakeProfileRepository(sayProfile),
          ),
          ...fakeAuthOverrides(
            FakeAuthRepository(),
            MemorySessionStore(refreshToken: 'refresh-old'),
          ),
          analysisRepositoryProvider.overrideWithValue(repository),
          analysisCacheProvider.overrideWithValue(cache),
        ],
      );
      addTearDown(c.dispose);
      return c;
    }

    test(
      'downloads once, then revalidates the cached copy with its etag',
      () async {
        final db = memoryDatabase();
        addTearDown(db.close);
        final cache = DriftAnalysisCache(CacheStore(db));
        final repository = FakeAnalysisRepository();

        final first = container(db, repository, cache);
        expect(
          (await first.read(analysisOverviewProvider.future))!.etag,
          '"e1"',
        );
        first.dispose();

        repository.overview = overviewJson(
          etag: '"e2"',
          topics: [topicStatusJson('TYT.TUR-1')],
        );
        final second = container(db, repository, cache);
        expect(
          (await second.read(analysisOverviewProvider.future))!.etag,
          '"e1"',
        );
        await Future<void>.delayed(Duration.zero);
        await Future<void>.delayed(Duration.zero);

        expect(repository.overviewRequests, [null, '"e1"']);
        expect(second.read(analysisOverviewProvider).value!.etag, '"e2"');
        expect(
          second.read(topicStatusProvider('TYT.TUR-1'))?.band,
          MasteryBand.weak,
        );
        expect((await cache.read(me))!.etag, '"e2"');
      },
    );

    test(
      'offline keeps the cached panel; without one it is an error',
      () async {
        final db = memoryDatabase();
        addTearDown(db.close);
        final cache = DriftAnalysisCache(CacheStore(db));
        final repository = FakeAnalysisRepository()
          ..error = const AnalysisException('offline', isOffline: true);

        final empty = container(db, repository, cache);
        empty.listen(analysisOverviewProvider, (_, _) {});
        for (
          var i = 0;
          i < 20 && empty.read(analysisOverviewProvider).isLoading;
          i++
        ) {
          await pumpEventQueue();
        }
        expect(
          empty.read(analysisOverviewProvider).error,
          isA<AnalysisException>(),
        );
        empty.dispose();

        await cache.write(me, AnalysisOverview.fromJson(overviewJson()));
        final cached = container(db, repository, cache);
        expect(
          (await cached.read(analysisOverviewProvider.future))!.etag,
          '"e1"',
        );
        await Future<void>.delayed(Duration.zero);
        expect(cached.read(analysisOverviewProvider).value!.etag, '"e1"');
      },
    );
  });
}
