import 'package:dio/dio.dart';

import '../../../core/network/api_problem.dart';
import '../domain/curriculum.dart';
import '../domain/curriculum_repository.dart';

class HttpCurriculumRepository implements CurriculumRepository {
  HttpCurriculumRepository(this._dio);

  final Dio _dio;

  @override
  Future<Curriculum?> fetch({String? ifNoneMatch}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/v1/curriculum',
        options: Options(
          headers: {'If-None-Match': ?_quoted(ifNoneMatch)},
          validateStatus: (s) => s != null && (s == 304 || s ~/ 100 == 2),
        ),
      );
      return response.statusCode == 304
          ? null
          : Curriculum.fromJson(response.data!);
    } on DioException catch (e) {
      final problem = ApiProblem.fromDio(e);
      throw CurriculumException(
        problem.isNetworkError
            ? 'Sunucuya ulaşılamadı; bağlantını kontrol et.'
            : 'Konular yüklenemedi; tekrar dene.',
      );
    }
  }

  static String? _quoted(String? version) =>
      version == null ? null : '"$version"';
}
