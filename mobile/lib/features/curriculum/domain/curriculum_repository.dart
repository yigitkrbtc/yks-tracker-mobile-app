import 'curriculum.dart';

/// Curriculum API. Every method throws [CurriculumException] on failure.
abstract interface class CurriculumRepository {
  /// Null when [ifNoneMatch] still matches the server's version (nothing to download).
  Future<Curriculum?> fetch({String? ifNoneMatch});
}

class CurriculumException implements Exception {
  const CurriculumException(this.message);

  final String message;

  @override
  String toString() => 'CurriculumException($message)';
}
