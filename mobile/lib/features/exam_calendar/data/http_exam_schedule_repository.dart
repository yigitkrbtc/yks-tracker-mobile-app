import 'package:dio/dio.dart';

import '../../../core/database/cache_store.dart';
import '../domain/exam_schedule.dart';
import '../domain/exam_schedule_repository.dart';

class HttpExamScheduleRepository implements ExamScheduleRepository {
  HttpExamScheduleRepository(this._dio);

  final Dio _dio;

  @override
  Future<ExamSchedule?> fetch({String? ifNoneMatch}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/v1/curriculum/exam-calendar',
        options: Options(
          headers: {if (ifNoneMatch != null) 'If-None-Match': '"$ifNoneMatch"'},
          validateStatus: (s) => s != null && (s == 304 || s ~/ 100 == 2),
        ),
      );
      return response.statusCode == 304
          ? null
          : ExamSchedule.fromJson(response.data!);
    } on DioException {
      throw const ExamScheduleException('Sınav takvimi yüklenemedi.');
    }
  }
}

class DriftExamScheduleCache implements ExamScheduleCache {
  DriftExamScheduleCache(this._store);

  static const key = 'exam_calendar';

  final CacheStore _store;

  @override
  Future<ExamSchedule?> read() async {
    final json = await _store.read(key);
    try {
      return json == null ? null : ExamSchedule.fromJson(json);
    } on Object {
      return null;
    }
  }

  @override
  Future<void> write(ExamSchedule schedule) =>
      _store.write(key, schedule.toJson());
}
