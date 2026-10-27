import 'package:dio/dio.dart';

/// An RFC 9457 problem from the API. [type] is the stable error code (e.g. `identity.invalid_code`).
class ApiProblem implements Exception {
  const ApiProblem({
    required this.status,
    this.type,
    this.title,
    this.fieldErrors = const {},
  });

  /// HTTP status; 0 when the server was not reached.
  final int status;
  final String? type;
  final String? title;
  final Map<String, List<String>> fieldErrors;

  bool get isNetworkError => status == 0;

  factory ApiProblem.fromDio(DioException error) {
    final response = error.response;
    if (response == null) {
      return const ApiProblem(status: 0);
    }

    final body = response.data;
    if (body is! Map<String, dynamic>) {
      return ApiProblem(status: response.statusCode ?? 0);
    }

    final errors = <String, List<String>>{};
    final rawErrors = body['errors'];
    if (rawErrors is Map<String, dynamic>) {
      for (final entry in rawErrors.entries) {
        final messages = entry.value;
        if (messages is List<dynamic>) {
          errors[entry.key] = messages.whereType<String>().toList();
        }
      }
    }

    return ApiProblem(
      status: response.statusCode ?? 0,
      type: body['type'] as String?,
      title: body['title'] as String?,
      fieldErrors: errors,
    );
  }

  @override
  String toString() => 'ApiProblem($status, $type)';
}
