import 'package:dio/dio.dart';

import '../../../core/database/cache_store.dart';
import '../../../core/network/api_problem.dart';
import '../domain/recommendation_repository.dart';
import '../domain/weekly_plan.dart';

class HttpRecommendationRepository implements RecommendationRepository {
  HttpRecommendationRepository(this._dio);

  final Dio _dio;

  @override
  Future<WeeklyPlan?> fetchWeek({String? ifNoneMatch}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/v1/recommendations/week',
        options: Options(
          headers: {'If-None-Match': ?ifNoneMatch},
          validateStatus: (s) => s != null && (s == 304 || s ~/ 100 == 2),
        ),
      );
      return response.statusCode == 304
          ? null
          : WeeklyPlan.fromJson(response.data!);
    } on DioException catch (e) {
      throw _failure(e, 'Plan yüklenemedi; tekrar dene.');
    }
  }

  @override
  Future<WeeklyPlan> dismiss(String itemKey, DismissReason reason) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        '/v1/recommendations/items/${Uri.encodeComponent(itemKey)}/dismiss',
        data: {'reason': reason.code},
      );
      return WeeklyPlan.fromJson(response.data!);
    } on DioException catch (e) {
      throw _failure(e, 'Öneri kaldırılamadı; tekrar dene.');
    }
  }

  static RecommendationException _failure(DioException e, String message) {
    final problem = ApiProblem.fromDio(e);
    return problem.isNetworkError
        ? const RecommendationException(
            'Sunucuya ulaşılamadı; internete bağlanınca tekrar dene.',
            isOffline: true,
          )
        : RecommendationException(problem.title ?? message);
  }
}

class DriftRecommendationCache implements RecommendationCache {
  DriftRecommendationCache(this._store);

  static String keyFor(String userId) => 'weekly_plan:$userId';

  final CacheStore _store;

  @override
  Future<WeeklyPlan?> read(String userId) async {
    final json = await _store.read(keyFor(userId));
    try {
      return json == null ? null : WeeklyPlan.fromJson(json);
    } on Object {
      return null;
    }
  }

  @override
  Future<void> write(String userId, WeeklyPlan plan) =>
      _store.write(keyFor(userId), plan.toJson());
}
