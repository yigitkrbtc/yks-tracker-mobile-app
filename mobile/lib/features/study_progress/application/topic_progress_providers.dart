import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../curriculum/application/curriculum_providers.dart';
import '../domain/topic_progress.dart';
import '../domain/topic_progress_repository.dart';

/// Bound to the Drift implementation in `app/composition.dart`; tests use it over an in-memory database.
final topicProgressRepositoryProvider = Provider<TopicProgressRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

final topicProgressProvider = StreamProvider<Map<String, ProgressStatus>>(
  (ref) => ref.watch(topicProgressRepositoryProvider).watchAll(),
);

final topicMarkProvider = Provider.family<ProgressStatus?, String>(
  (ref, topicId) => ref.watch(topicProgressProvider).value?[topicId],
);

/// Progress over the topics of the student's score types; null until curriculum and profile are known.
final curriculumProgressProvider = Provider<CurriculumProgress?>((ref) {
  final subjects = ref.watch(visibleSubjectsProvider).value;
  final marks = ref.watch(topicProgressProvider).value;
  if (subjects == null || marks == null) {
    return null;
  }
  return CurriculumProgress.of([
    for (final s in subjects)
      for (final t in s.topics)
        (id: t.id, expectedQuestions: t.expectedQuestions),
  ], marks);
});
