import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/mock_exams/domain/mock_exam.dart';

import '../../support/app_fakes.dart';

void main() {
  final curriculum = seedCurriculum();
  final now = DateTime.utc(2026, 10, 31, 9);

  MockExam exam(
    List<MockSectionResult> sections, {
    MockExamKind kind = MockExamKind.tyt,
    List<TopicMark> marks = const [],
  }) => MockExam(
    id: 'e1',
    kind: kind,
    takenOn: DateTime.utc(2026, 10, 30),
    sourceId: 'src-pub',
    sections: sections,
    marks: marks,
    modifiedAt: now,
  );

  MockSectionResult s(String code, int q, int c, int w) => MockSectionResult(
    sectionCode: code,
    questionCount: q,
    correct: c,
    wrong: w,
  );

  List<String> errors(MockExam e) => MockExamRules.validate(e, curriculum, now);

  test('a valid TYT exam passes', () {
    expect(
      errors(exam([s('TYT-TUR', 40, 30, 8), s('TYT-SOS.TAR', 5, 3, 1)])),
      isEmpty,
    );
  });

  test('TYT sections keep the ÖSYM count; a branch exam may change it', () {
    expect(errors(exam([s('TYT-TUR', 30, 20, 5)])), [
      'Türkçe bölümü 40 sorudur.',
    ]);
    expect(
      errors(exam([s('TYT-TUR', 30, 20, 5)], kind: MockExamKind.branch)),
      isEmpty,
    );
    expect(
      errors(
        exam([
          s('TYT-TUR', 40, 20, 5),
          s('TYT-MAT', 40, 1, 1),
        ], kind: MockExamKind.branch),
      ),
      ['Branş denemesi tek bölümdür.'],
    );
  });

  test('a block and its subject, or religion and its replacement, do not mix', () {
    const overlap =
        'Bir blok ya toplam olarak ya da dersleriyle girilir; Din ve yerine çözülen felsefe birlikte olmaz.';
    expect(errors(exam([s('TYT-SOS', 20, 10, 2), s('TYT-SOS.TAR', 5, 3, 1)])), [
      overlap,
    ]);
    expect(
      errors(exam([s('TYT-SOS.DKAB', 5, 3, 1), s('TYT-SOS.FEL2', 5, 3, 1)])),
      [overlap],
    );
  });

  test('marks cannot exceed what the section lost', () {
    final e = exam(
      [s('TYT-SOS.TAR', 5, 3, 1)],
      marks: const [
        TopicMark(sectionCode: 'TYT-SOS.TAR', topicId: 'a', wrong: 2, blank: 0),
      ],
    );
    expect(errors(e), [
      'Etiketlenen yanlış ve boşlar bölümün yanlış ve boş sayısını aşamaz.',
    ]);
  });

  test('offered sections follow the kind', () {
    final tyt = MockExamRules.sectionsFor(MockExamKind.tyt, curriculum);
    expect(tyt.every((x) => x.testCode.startsWith('TYT')), isTrue);
    expect(
      MockExamRules.sectionsFor(MockExamKind.branch, curriculum),
      hasLength(27),
    );
  });
}
