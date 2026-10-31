import 'dart:convert';
import 'dart:io';

import 'package:flutter_riverpod/misc.dart';
import 'package:nirengi/core/database/database_providers.dart';
import 'package:nirengi/core/network/api_client.dart';
import 'package:nirengi/features/curriculum/application/curriculum_providers.dart';
import 'package:nirengi/features/curriculum/domain/curriculum.dart';
import 'package:nirengi/features/curriculum/domain/curriculum_repository.dart';
import 'package:nirengi/features/profile/application/profile_providers.dart';
import 'package:nirengi/features/profile/domain/profile_repository.dart';
import 'package:nirengi/features/profile/domain/study_profile.dart';

import 'auth_fakes.dart';
import 'sync_fakes.dart';

const sayProfile = StudyProfile(
  grade: Grade.twelfth,
  primaryScoreType: ScoreType.say,
  targetExamYear: 2027,
);

class FakeProfileRepository implements ProfileRepository {
  FakeProfileRepository([this.profile]);

  StudyProfile? profile;
  final saved = <StudyProfile>[];
  ProfileException? saveError;

  @override
  Future<StudyProfile?> get() async => profile;

  @override
  Future<StudyProfile> save(StudyProfile profile) async {
    if (saveError != null) {
      throw saveError!;
    }
    saved.add(profile);
    return this.profile = profile;
  }
}

class FakeCurriculumRepository implements CurriculumRepository {
  FakeCurriculumRepository([Curriculum? curriculum])
    : curriculum = curriculum ?? seedCurriculum();

  Curriculum curriculum;
  final requests = <String?>[];

  @override
  Future<Curriculum?> fetch({String? ifNoneMatch}) async {
    requests.add(ifNoneMatch);
    return ifNoneMatch == curriculum.version ? null : curriculum;
  }
}

/// The real exam structure from data/seed, with two topics per subject, as the API would serve it.
Curriculum seedCurriculum() {
  final structure = jsonDecode(
    File('../data/seed/exam_structure.json').readAsStringSync(),
  ) as Map<String, dynamic>;
  final tests = {
    for (final t in structure['tests'] as List<dynamic>)
      (t as Map<String, dynamic>)['code']: t['session'],
  };
  return Curriculum.fromJson({
    'version': 'v1',
    'examYear': structure['examYear'],
    'scoreTypes': [
      for (final s in structure['scoreTypes'] as List<dynamic>)
        {'code': (s as Map<String, dynamic>)['code'], 'testCodes': s['tests']},
    ],
    'subjects': [
      for (final u in structure['units'] as List<dynamic>)
        {
          'code': (u as Map<String, dynamic>)['code'],
          'session': tests[(u['tests'] as List<dynamic>).first],
          'name': u['name'],
          'testCodes': u['tests'],
          'topics': [
            for (var i = 1; i <= 2; i++)
              {
                'id': '${u['code']}-$i',
                'code': '${u['code']}.0$i',
                'name': '${u['name']} konu $i',
                'subtopics': ['Alt konu'],
                'gradeMin': 9,
                'gradeMax': 10,
                'expectedQuestions': i == 1 ? 2.4 : 0,
                'tier': i == 1 ? 'High' : 'Rare',
              },
          ],
        },
    ],
  });
}

/// Auth fakes plus profile and curriculum fakes: everything the app shell needs.
List<Override> fakeAppOverrides({
  FakeAuthRepository? auth,
  required MemorySessionStore store,
  FakeProfileRepository? profiles,
  FakeCurriculumRepository? curriculum,
  FakeApi? api,
}) => [
  ...fakeAuthOverrides(auth ?? FakeAuthRepository(), store),
  appDatabaseProvider.overrideWith((ref) {
    final db = memoryDatabase();
    ref.onDispose(db.close);
    return db;
  }),
  apiClientProvider.overrideWithValue((api ?? FakeApi()).dio),
  profileRepositoryProvider.overrideWithValue(
    profiles ?? FakeProfileRepository(sayProfile),
  ),
  curriculumRepositoryProvider.overrideWithValue(
    curriculum ?? FakeCurriculumRepository(),
  ),
];
