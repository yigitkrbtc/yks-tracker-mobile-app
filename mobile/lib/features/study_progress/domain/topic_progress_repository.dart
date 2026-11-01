import 'topic_progress.dart';

/// Marks on the device, synced in the background (ADR-0014). Unmarked topics are absent from the map.
abstract interface class TopicProgressRepository {
  Stream<Map<String, ProgressStatus>> watchAll();

  Future<void> mark(String topicId, ProgressStatus status);
}
