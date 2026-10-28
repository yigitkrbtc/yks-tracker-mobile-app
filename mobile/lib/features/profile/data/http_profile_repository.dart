import 'package:dio/dio.dart';

import '../../../core/network/api_problem.dart';
import '../domain/profile_repository.dart';
import '../domain/study_profile.dart';

class HttpProfileRepository implements ProfileRepository {
  HttpProfileRepository(this._dio);

  final Dio _dio;

  @override
  Future<StudyProfile?> get() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/v1/profile');
      return StudyProfile.fromJson(response.data!);
    } on DioException catch (e) {
      final problem = ApiProblem.fromDio(e);
      if (problem.type == 'tracking.profile_not_found') {
        return null;
      }
      throw _exception(problem);
    }
  }

  @override
  Future<StudyProfile> save(StudyProfile profile) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(
        '/v1/profile',
        data: profile.toJson(),
      );
      return StudyProfile.fromJson(response.data!);
    } on DioException catch (e) {
      throw _exception(ApiProblem.fromDio(e));
    }
  }

  static ProfileException _exception(ApiProblem problem) {
    if (problem.isNetworkError) {
      return const ProfileException(
        'Sunucuya ulaşılamadı; bağlantını kontrol et.',
        isNetworkError: true,
      );
    }
    final fieldMessages = problem.fieldErrors.values.expand((m) => m);
    if (fieldMessages.isNotEmpty) {
      return ProfileException(fieldMessages.join('\n'));
    }
    return ProfileException(
      problem.title ?? 'Bir şeyler ters gitti; tekrar dene.',
    );
  }
}
