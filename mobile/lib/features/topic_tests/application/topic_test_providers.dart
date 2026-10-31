import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../domain/topic_test.dart';
import '../domain/topic_test_repository.dart';

/// Bound to the device database in `app/composition.dart`.
final topicTestRepositoryProvider = Provider<TopicTestRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

/// The student's tests, newest day first; updates on local writes and on sync.
final topicTestsProvider = StreamProvider<List<TopicTest>>(
  (ref) => ref.watch(topicTestRepositoryProvider).watchAll(),
);

/// Ids for new tests: UUID v7, so the server and other devices accept the device's id (ADR-0005).
final newTopicTestIdProvider = Provider<String Function()>(
  (ref) =>
      () => const Uuid().v7(),
);
