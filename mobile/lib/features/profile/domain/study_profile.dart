/// ÖSYM score types. [code] is the API value and matches the curriculum's score type codes.
enum ScoreType {
  say('SAY', 'Sayısal'),
  ea('EA', 'Eşit Ağırlık'),
  soz('SOZ', 'Sözel'),
  dil('DIL', 'Dil');

  const ScoreType(this.code, this.label);

  final String code;
  final String label;

  static ScoreType fromCode(String code) =>
      values.firstWhere((t) => t.code == code);
}

enum Grade {
  ninth('Ninth', '9. sınıf'),
  tenth('Tenth', '10. sınıf'),
  eleventh('Eleventh', '11. sınıf'),
  twelfth('Twelfth', '12. sınıf'),
  graduate('Graduate', 'Mezun');

  const Grade(this.code, this.label);

  final String code;
  final String label;

  /// YKS sittings until the student's first regular exam (a graduate sits the next one).
  int get sittingsAhead => switch (this) {
    ninth => 3,
    tenth => 2,
    eleventh => 1,
    twelfth || graduate => 0,
  };

  static Grade fromCode(String code) =>
      values.firstWhere((g) => g.code == code);
}

enum YdtLanguage {
  english('English', 'İngilizce'),
  german('German', 'Almanca'),
  french('French', 'Fransızca'),
  russian('Russian', 'Rusça'),
  arabic('Arabic', 'Arapça');

  const YdtLanguage(this.code, this.label);

  final String code;
  final String label;

  static YdtLanguage fromCode(String code) =>
      values.firstWhere((l) => l.code == code);
}

/// What the student prepares for; decides which topics they see.
class StudyProfile {
  const StudyProfile({
    required this.grade,
    required this.primaryScoreType,
    this.secondaryScoreType,
    this.ydtLanguage,
    required this.targetExamYear,
    this.weeklyQuestionGoal,
  });

  static const maxWeeklyQuestionGoal = 5000;

  final Grade grade;
  final ScoreType primaryScoreType;

  /// For students undecided between two types (typically SAY and EA).
  final ScoreType? secondaryScoreType;

  /// Set exactly when DIL is one of the score types.
  final YdtLanguage? ydtLanguage;
  final int targetExamYear;
  final int? weeklyQuestionGoal;

  List<ScoreType> get scoreTypes => [primaryScoreType, ?secondaryScoreType];

  bool get studiesLanguage => scoreTypes.contains(ScoreType.dil);

  Map<String, Object?> toJson() => {
    'grade': grade.code,
    'primaryScoreType': primaryScoreType.code,
    'secondaryScoreType': secondaryScoreType?.code,
    'ydtLanguage': ydtLanguage?.code,
    'targetExamYear': targetExamYear,
    'weeklyQuestionGoal': weeklyQuestionGoal,
  };

  factory StudyProfile.fromJson(Map<String, dynamic> json) {
    final secondary = json['secondaryScoreType'] as String?;
    final language = json['ydtLanguage'] as String?;
    return StudyProfile(
      grade: Grade.fromCode(json['grade'] as String),
      primaryScoreType: ScoreType.fromCode(json['primaryScoreType'] as String),
      secondaryScoreType: secondary == null
          ? null
          : ScoreType.fromCode(secondary),
      ydtLanguage: language == null ? null : YdtLanguage.fromCode(language),
      targetExamYear: json['targetExamYear'] as int,
      weeklyQuestionGoal: json['weeklyQuestionGoal'] as int?,
    );
  }
}
