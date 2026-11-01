import 'package:drift_flutter/drift_flutter.dart';
import 'package:flutter_riverpod/misc.dart';

import '../core/database/app_database.dart';
import '../core/database/cache_store.dart';
import '../core/database/database_providers.dart';

import '../core/network/api_client.dart';
import '../core/notifications/notification_providers.dart';
import '../core/notifications/plugin_reminder_scheduler.dart';
import '../core/notifications/local_reminder_scheduler.dart';
import '../core/network/session_tokens.dart';
import '../core/sync/sync_providers.dart';
import '../features/analytics/application/analysis_providers.dart';
import '../features/analytics/data/drift_analysis_cache.dart';
import '../features/analytics/data/http_analysis_repository.dart';
import '../features/auth/application/auth_providers.dart';
import '../features/auth/data/http_auth_repository.dart';
import '../features/auth/data/secure_session_store.dart';
import '../features/curriculum/application/curriculum_providers.dart';
import '../features/curriculum/data/drift_curriculum_cache.dart';
import '../features/curriculum/data/http_curriculum_repository.dart';
import '../features/data_export/application/data_export_providers.dart';
import '../features/data_export/data/http_data_export_service.dart';
import '../features/exam_calendar/application/exam_schedule_providers.dart';
import '../features/exam_calendar/data/http_exam_schedule_repository.dart';
import '../features/goals/application/goal_providers.dart';
import '../features/goals/data/drift_activity_repository.dart';
import '../features/goals/data/drift_reminder_settings_store.dart';
import '../features/mock_exams/application/mock_exam_providers.dart';
import '../features/mock_exams/data/drift_mock_exam_repository.dart';
import '../features/mock_exams/data/mock_exam_sync_handler.dart';
import '../features/profile/application/profile_providers.dart';
import '../features/profile/data/drift_profile_cache.dart';
import '../features/profile/data/http_profile_repository.dart';
import '../features/recommendations/application/recommendation_providers.dart';
import '../features/recommendations/data/http_recommendation_repository.dart';
import '../features/study_progress/application/topic_progress_providers.dart';
import '../features/study_progress/data/drift_topic_progress_repository.dart';
import '../features/study_progress/data/topic_progress_sync_handler.dart';
import '../features/topic_tests/application/topic_test_providers.dart';
import '../features/topic_tests/data/drift_topic_test_repository.dart';
import '../features/topic_tests/data/topic_test_sync_handler.dart';

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
  analysisRepositoryProvider.overrideWith(
    (ref) => HttpAnalysisRepository(ref.watch(apiClientProvider)),
  ),
  analysisCacheProvider.overrideWith(
    (ref) => DriftAnalysisCache(CacheStore(ref.watch(appDatabaseProvider))),
  ),
  recommendationRepositoryProvider.overrideWith(
    (ref) => HttpRecommendationRepository(ref.watch(apiClientProvider)),
  ),
  recommendationCacheProvider.overrideWith(
    (ref) =>
        DriftRecommendationCache(CacheStore(ref.watch(appDatabaseProvider))),
  ),
  examScheduleRepositoryProvider.overrideWith(
    (ref) => HttpExamScheduleRepository(ref.watch(publicApiProvider)),
  ),
  examScheduleCacheProvider.overrideWith(
    (ref) => DriftExamScheduleCache(CacheStore(ref.watch(appDatabaseProvider))),
  ),
  reminderSettingsStoreProvider.overrideWith(
    (ref) =>
        DriftReminderSettingsStore(CacheStore(ref.watch(appDatabaseProvider))),
  ),
  localReminderSchedulerProvider.overrideWith(
    (ref) => SerialReminderScheduler(PluginReminderScheduler()),
  ),
  dataExportServiceProvider.overrideWith(
    (ref) => HttpDataExportService(ref.watch(apiClientProvider)),
  ),
  ...sessionOverrides,
  ...offlineRecordOverrides,
];

/// Records written on the device and synced (ADR-0014). Tests use these too, over an in-memory database.
final List<Override> offlineRecordOverrides = [
  topicTestRepositoryProvider.overrideWith(
    (ref) => DriftTopicTestRepository(
      ref.watch(appDatabaseProvider),
      ref.watch(outboxStoreProvider),
    ),
  ),
  mockExamRepositoryProvider.overrideWith(
    (ref) => DriftMockExamRepository(
      ref.watch(appDatabaseProvider),
      ref.watch(outboxStoreProvider),
    ),
  ),
  topicProgressRepositoryProvider.overrideWith(
    (ref) => DriftTopicProgressRepository(
      ref.watch(appDatabaseProvider),
      ref.watch(outboxStoreProvider),
    ),
  ),
  activityRepositoryProvider.overrideWith(
    (ref) => DriftActivityRepository(ref.watch(appDatabaseProvider)),
  ),
  syncHandlersProvider.overrideWith(
    (ref) => [
      TopicTestSyncHandler(
        ref.watch(appDatabaseProvider),
        ref.watch(outboxStoreProvider),
      ),
      MockExamSyncHandler(
        ref.watch(appDatabaseProvider),
        ref.watch(outboxStoreProvider),
      ),
      TopicProgressSyncHandler(
        ref.watch(appDatabaseProvider),
        ref.watch(outboxStoreProvider),
      ),
    ],
  ),
];

/// Lets core's HTTP client use the auth feature's tokens without importing the feature.
final List<Override> sessionOverrides = [
  sessionTokensProvider.overrideWith(
    (ref) => ref.watch(sessionTokenManagerProvider),
  ),
];
