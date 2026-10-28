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
}
