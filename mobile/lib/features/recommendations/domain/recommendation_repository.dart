import 'weekly_plan.dart';

/// Recommendation API. Every method throws [RecommendationException] on failure.
abstract interface class RecommendationRepository {
  /// Null when [ifNoneMatch] still matches the server's plan.
  Future<WeeklyPlan?> fetchWeek({String? ifNoneMatch});

  /// Drops an item; the answer is the plan with the next candidate in its place.
  Future<WeeklyPlan> dismiss(String itemKey, DismissReason reason);
}

/// The last downloaded plan per student, shown offline.
abstract interface class RecommendationCache {
  Future<WeeklyPlan?> read(String userId);

  Future<void> write(String userId, WeeklyPlan plan);
}

class RecommendationException implements Exception {
  const RecommendationException(this.message, {this.isOffline = false});

  final String message;
  final bool isOffline;

  @override
  String toString() => 'RecommendationException($message)';
}
