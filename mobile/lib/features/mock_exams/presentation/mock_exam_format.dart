import 'package:flutter/widgets.dart';

import '../../../core/design/design.dart';
import '../../curriculum/domain/curriculum.dart';
import '../domain/mock_exam.dart';

/// The publisher as the student sees it: the listed name, or what they typed.
String publisherLabel(MockExam e, Curriculum? curriculum) {
  final id = e.sourceId;
  if (id == null) {
    return e.sourceName ?? 'Yayınevi yok';
  }
  for (final s in curriculum?.sources ?? const <QuestionSource>[]) {
    if (s.id == id) {
      return s.name;
    }
  }
  return 'Yayınevi';
}

/// Section name with its test when it is a subject line: "Sosyal Bilimler · Tarih".
String sectionLabel(String code, Curriculum? curriculum) {
  final sections = {
    for (final s in curriculum?.mockSections ?? const <MockSection>[])
      s.code: s,
  };
  final section = sections[code];
  if (section == null) {
    return code;
  }
  final parent = sections[section.parentCode];
  return parent == null ? section.name : '${parent.name} · ${section.name}';
}

/// [exam]'s sections in score sheet order; codes no longer in the template go last.
List<MockSectionResult> orderedSections(MockExam exam, Curriculum? curriculum) {
  final order = {
    for (final (i, s)
        in (curriculum?.mockSections ?? const <MockSection>[]).indexed)
      s.code: i,
  };
  return [...exam.sections]..sort(
    (a, b) => (order[a.sectionCode] ?? 1 << 20).compareTo(
      order[b.sectionCode] ?? 1 << 20,
    ),
  );
}

/// Shown only when the exam is not on the server yet.
class MockExamSyncBadge extends StatelessWidget {
  const MockExamSyncBadge({super.key, required this.status});

  final SyncStatus status;

  @override
  Widget build(BuildContext context) => switch (status) {
    SyncStatus.synced => const SizedBox.shrink(),
    SyncStatus.pending => const NBadge(
      'Gönderilmeyi bekliyor',
      tone: NBadgeTone.warning,
    ),
    SyncStatus.rejected => const NBadge(
      'Sunucu kabul etmedi',
      tone: NBadgeTone.danger,
    ),
  };
}

String taggingLabel(TaggingStatus status) => switch (status) {
  TaggingStatus.none => 'Etiketlenmedi',
  TaggingStatus.partial => 'Kısmen etiketli',
  TaggingStatus.full => 'Etiketli',
};
