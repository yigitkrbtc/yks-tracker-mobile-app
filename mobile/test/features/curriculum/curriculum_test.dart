import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/curriculum/domain/curriculum.dart';

import '../../support/app_fakes.dart';

void main() {
  final curriculum = seedCurriculum();

  List<String> subjectsFor(List<String> scoreTypes) =>
      curriculum.subjectsFor(scoreTypes).map((s) => s.code).toList();

  const tyt = [
    'TYT.TUR',
    'TYT.TMAT',
    'TYT.GEO',
    'TYT.TAR',
    'TYT.COG',
    'TYT.FEL',
    'TYT.DKAB',
    'TYT.FIZ',
    'TYT.KIM',
    'TYT.BIY',
  ];

  test('SAY studies all of TYT plus AYT mathematics and science', () {
    expect(subjectsFor(['SAY']), [
      ...tyt,
      'AYT.AMAT',
      'AYT.GEO',
      'AYT.FIZ',
      'AYT.KIM',
      'AYT.BIY',
    ]);
  });

  test('EA gets literature, all of AYT history and geography, and maths', () {
    expect(subjectsFor(['EA']), [
      ...tyt,
      'AYT.AMAT',
      'AYT.GEO',
      'AYT.EDB',
      'AYT.TAR',
      'AYT.COG',
    ]);
  });

  test('SOZ adds philosophy and religion from SB2', () {
    expect(subjectsFor(['SOZ']), [
      ...tyt,
      'AYT.EDB',
      'AYT.TAR',
      'AYT.COG',
      'AYT.FEL',
      'AYT.DKAB',
    ]);
  });

  test('DIL sees only TYT until the YDT taxonomy exists', () {
    expect(subjectsFor(['DIL']), tyt);
  });

  test('a second score type adds its subjects without duplicates', () {
    final both = subjectsFor(['SAY', 'EA']);
    expect(both.toSet().length, both.length);
    expect(both, containsAll(['AYT.FIZ', 'AYT.EDB', 'AYT.TAR']));
  });

  test('topics parse weights and grade labels', () {
    final topic = curriculum.subjects.first.topics.first;
    expect(topic.tier, WeightTier.high);
    expect(topic.expectedQuestions, 2.4);
    expect(topic.gradeLabel, '9–10. sınıf');
    expect(curriculum.subjects.first.topics.last.notAskedRecently, isTrue);
  });

  test('AYT history weight counts once for EA and twice for SOZ', () {
    const topic = Topic(
      id: 't',
      code: 'AYT.TAR.01',
      name: 'Tarih',
      subtopics: ['x'],
      gradeMin: 9,
      gradeMax: 9,
      expectedQuestions: 1.9,
      tier: WeightTier.medium,
      weights: [
        TopicWeight(
          testCode: 'AYT-TDE-SB1',
          expectedQuestions: 0.8,
          tier: WeightTier.medium,
        ),
        TopicWeight(
          testCode: 'AYT-SB2',
          expectedQuestions: 1.1,
          tier: WeightTier.medium,
        ),
      ],
    );
    final ea = curriculum.testsFor(['EA']);
    final soz = curriculum.testsFor(['SOZ']);

    expect(topic.forTests(ea).expectedQuestions, 0.8);
    expect(topic.forTests(soz).expectedQuestions, 1.9);
    expect(topic.forTests({'AYT-MAT'}).notAskedRecently, isTrue);
    expect(topic.forTests({'AYT-MAT'}).tier, WeightTier.rare);
    expect(Topic.fromJson(topic.toJson()).weights.length, 2);
  });

  test('a topic cached without per-test weights keeps its total', () {
    final json = curriculum.subjects.first.topics.first.toJson()
      ..remove('weights');
    final topic = Topic.fromJson(json);
    expect(topic.weights, isEmpty);
    expect(topic.forTests({'AYT-MAT'}).expectedQuestions, 2.4);
  });
}
