import 'package:dio/dio.dart';

import '../../../core/network/api_problem.dart';
import '../domain/analysis.dart';
import '../domain/analysis_repository.dart';

class HttpAnalysisRepository implements AnalysisRepository {
  HttpAnalysisRepository(this._dio);

  final Dio _dio;

  @override
  Future<AnalysisOverview?> fetchOverview({String? ifNoneMatch}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/v1/analytics/overview',
        options: Options(
          headers: {'If-None-Match': ?ifNoneMatch},
          validateStatus: (s) => s != null && (s == 304 || s ~/ 100 == 2),
        ),
      );
      return response.statusCode == 304
          ? null
          : AnalysisOverview.fromJson(response.data!);
    } on DioException catch (e) {
      throw _failure(e);
    }
  }

  @override
  Future<TopicAnalysis> fetchTopic(String topicId) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/v1/analytics/topics/$topicId',
      );
      return TopicAnalysis.fromJson(response.data!);
    } on DioException catch (e) {
      throw _failure(e);
    }
  }

  static AnalysisException _failure(DioException e) {
    final problem = ApiProblem.fromDio(e);
    return problem.isNetworkError
        ? const AnalysisException(
            'Sunucuya ulaşılamadı; son analiz gösteriliyor.',
            isOffline: true,
          )
        : const AnalysisException('Analiz yüklenemedi; tekrar dene.');
  }
}
