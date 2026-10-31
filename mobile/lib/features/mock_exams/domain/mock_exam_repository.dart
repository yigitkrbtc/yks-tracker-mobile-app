import 'mock_exam.dart';

/// Mock exams on this device. Writes never wait for the network: they are stored and queued for sync.
abstract interface class MockExamRepository {
  /// Exams not deleted, newest day first.
  Stream<List<MockExam>> watchAll();

  Future<MockExam?> get(String id);

  /// Stores [exam] as the newest version (its edit time is set to now) and queues it, marks included.
  Future<void> save(MockExam exam);

  Future<void> delete(String id);

  /// The publisher of the most recently edited exam, to preselect it.
  Future<({String? id, String? name})?> lastSource();
}
