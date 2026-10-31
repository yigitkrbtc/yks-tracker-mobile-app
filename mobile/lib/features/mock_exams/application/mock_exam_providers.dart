import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../domain/mock_exam.dart';
import '../domain/mock_exam_repository.dart';

/// Bound to the device database in `app/composition.dart`.
final mockExamRepositoryProvider = Provider<MockExamRepository>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);

/// The student's mock exams, newest day first; updates on local writes and on sync.
final mockExamsProvider = StreamProvider<List<MockExam>>(
  (ref) => ref.watch(mockExamRepositoryProvider).watchAll(),
);

/// Ids for new exams: UUID v7 (ADR-0005).
final newMockExamIdProvider = Provider<String Function()>(
  (ref) =>
      () => const Uuid().v7(),
);
