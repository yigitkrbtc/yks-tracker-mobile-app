import 'package:nirengi/features/analytics/domain/analysis.dart';
import 'package:nirengi/features/analytics/domain/analysis_repository.dart';

/// Server analysis as JSON, shaped like `GET /v1/analytics/overview`.
Map<String, dynamic> overviewJson({
  String etag = '"e1"',
  List<Map<String, dynamic>> priorities = const [],
  List<String> lowDataTopics = const ['TYT.TUR-1', 'TYT.TMAT-1'],
  List<Map<String, dynamic>> sections = const [],
  List<Map<String, dynamic>> topics = const [],
  List<Map<String, dynamic>> tests = const [],
  List<Map<String, dynamic>> subjects = const [],
}) => {
  'eTag': etag,
  'computedAt': '2026-10-31T09:30:00Z',
  'computedOn': '2026-10-31',
  'ruleSetVersion': '2026.1',
  'invalidRecords': 0,
  'tests': tests,
  'subjects': subjects,
  'priorities': priorities,
  'lowDataTopics': lowDataTopics,
  'sections': sections,
  'topics': topics,
};

Map<String, dynamic> aggregateJson(
  String code, {
  bool isActive = true,
  double mastery = 0.62,
  int studiedTopics = 1,
}) => {
  'code': code,
  'isActive': isActive,
  'mastery': mastery,
  'masteryLow': mastery - 0.1,
  'masteryHigh': mastery + 0.1,
  'coverage': 0.4,
  'studiedTopics': studiedTopics,
  'topicCount': 2,
};

Map<String, dynamic> trendJson(String label, [double? up]) => {
  'label': label,
  'probabilityUp': up,
  'difference': up == null ? null : 0.1,
};

Map<String, dynamic> topicStatusJson(
  String id, {
  double mastery = 0.42,
  String band = 'Weak',
}) => {
  'topicId': id,
  'mastery': mastery,
  'masteryLow': mastery - 0.1,
  'masteryHigh': mastery + 0.1,
  'confidenceLevel': 'Reasonable',
  'band': band,
  'trend': 'Stable',
};

Map<String, dynamic> sectionJson(
  String code, {
  int exams = 4,
  bool gapWarning = false,
}) => {
  'code': code,
  'exams': exams,
  'isSufficient': exams >= 3,
  'accuracy': 0.7,
  'accuracyLow': 0.64,
  'accuracyHigh': 0.76,
  'markingRate': 0.9,
  'expectedNet': 26.3,
  'expectedNetLow': 24.1,
  'expectedNetHigh': 28.0,
  'trend': trendJson(exams >= 3 ? 'Improving' : 'Insufficient', 0.93),
  'practiceGap': gapWarning ? 0.2 : null,
  'gapWarning': gapWarning,
};

Map<String, dynamic> topicCardJson(
  String id, {
  int records = 3,
  bool optimistic = false,
  Map<String, dynamic>? mockSignal,
}) => {
  'topicId': id,
  'computedAt': '2026-10-31T09:30:00Z',
  'ruleSetVersion': '2026.1',
  'analysis': {
    'mastery': records == 0 ? null : 0.55,
    'masteryLow': records == 0 ? null : 0.45,
    'masteryHigh': records == 0 ? null : 0.65,
    'markingRate': records == 0 ? null : 0.8,
    'optimistic': optimistic,
    'expectedNetPerQuestion': records == 0 ? null : 0.36,
    'confidence': records == 0 ? null : 0.5,
    'confidenceLevel': records == 0 ? 'None' : 'Preliminary',
    'questionsToNextLevel': 11,
    'band': records == 0 ? 'None' : 'Developing',
    'trend': trendJson(records == 0 ? 'None' : 'Declining', 0.08),
    'priority': null,
    'totals': {
      'records': records,
      'questions': records * 20,
      'correct': records * 11,
      'wrong': records * 6,
      'blank': records * 3,
    },
    'mockSignal': mockSignal,
    'recent': [
      for (var i = 0; i < records; i++)
        {
          'date': '2026-10-${30 - i}',
          'total': 20,
          'correct': 11,
          'wrong': 6,
          'blank': 3,
          'isRepeat': i == 1,
        },
    ],
  },
};

/// Serves a fixed panel and topic cards; null overview means "nothing recorded yet".
class FakeAnalysisRepository implements AnalysisRepository {
  FakeAnalysisRepository({
    Map<String, dynamic>? overview,
    this.cards = const {},
  }) : overview = overview ?? overviewJson();

  Map<String, dynamic> overview;
  final Map<String, Map<String, dynamic>> cards;
  final overviewRequests = <String?>[];
  final topicRequests = <String>[];
  AnalysisException? error;

  @override
  Future<AnalysisOverview?> fetchOverview({String? ifNoneMatch}) async {
    overviewRequests.add(ifNoneMatch);
    if (error != null) {
      throw error!;
    }
    return ifNoneMatch == overview['eTag']
        ? null
        : AnalysisOverview.fromJson(overview);
  }

  @override
  Future<TopicAnalysis> fetchTopic(String topicId) async {
    topicRequests.add(topicId);
    if (error != null) {
      throw error!;
    }
    return TopicAnalysis.fromJson(
      cards[topicId] ?? topicCardJson(topicId, records: 0),
    );
  }
}
