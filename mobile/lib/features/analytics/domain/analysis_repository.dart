import 'analysis.dart';

/// Analysis API. Every method throws [AnalysisException] on failure.
abstract interface class AnalysisRepository {
  /// Null when [ifNoneMatch] still matches the server's analysis (nothing changed).
  Future<AnalysisOverview?> fetchOverview({String? ifNoneMatch});

  Future<TopicAnalysis> fetchTopic(String topicId);
}

/// The last downloaded panel per student, kept on the device so it shows offline.
abstract interface class AnalysisCache {
  Future<AnalysisOverview?> read(String userId);

  Future<void> write(String userId, AnalysisOverview overview);
}

class AnalysisException implements Exception {
  const AnalysisException(this.message, {this.isOffline = false});

  final String message;
  final bool isOffline;

  @override
  String toString() => 'AnalysisException($message)';
}
