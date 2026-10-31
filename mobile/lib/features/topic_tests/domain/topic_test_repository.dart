import 'topic_test.dart';

/// Topic tests on this device. Writes never wait for the network: they are stored and queued for sync.
abstract interface class TopicTestRepository {
  /// Tests not deleted, newest day first.
  Stream<List<TopicTest>> watchAll();

  Future<TopicTest?> get(String id);

  /// Stores [test] as the newest version (its edit time is set to now) and queues it.
  Future<void> save(TopicTest test);

  Future<void> delete(String id);

  /// The source of the most recently edited test, to preselect it.
  Future<({String? id, String? name})?> lastSource();
}
