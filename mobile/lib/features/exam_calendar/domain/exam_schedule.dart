/// One sitting or milestone in ÖSYM's calendar (K12). Estimated until ÖSYM announces the year.
class ExamEvent {
  const ExamEvent({
    required this.code,
    required this.name,
    required this.startsAt,
    this.endsAt,
    required this.estimated,
  });

  factory ExamEvent.fromJson(Map<String, dynamic> json) => ExamEvent(
    code: json['code'] as String,
    name: json['name'] as String,
    startsAt: DateTime.parse(json['startsAt'] as String).toUtc(),
    endsAt: switch (json['endsAt']) {
      final String s => DateTime.parse(s).toUtc(),
      _ => null,
    },
    estimated: (json['status'] as String).toLowerCase() == 'estimated',
  );

  /// `tyt`, `ayt`, `ydt`, `applications`, `results`…
  final String code;
  final String name;
  final DateTime startsAt;
  final DateTime? endsAt;
  final bool estimated;

  bool get isSitting => code == 'tyt' || code == 'ayt' || code == 'ydt';

  Map<String, Object?> toJson() => {
    'code': code,
    'name': name,
    'startsAt': startsAt.toIso8601String(),
    'endsAt': endsAt?.toIso8601String(),
    'status': estimated ? 'Estimated' : 'Announced',
  };
}

class ExamYear {
  const ExamYear({
    required this.examYear,
    required this.sourceUrl,
    this.note,
    required this.events,
  });

  factory ExamYear.fromJson(Map<String, dynamic> json) => ExamYear(
    examYear: json['examYear'] as int,
    sourceUrl: json['sourceUrl'] as String,
    note: json['note'] as String?,
    events: [
      for (final e in json['events'] as List<dynamic>)
        ExamEvent.fromJson(e as Map<String, dynamic>),
    ],
  );

  final int examYear;
  final String sourceUrl;
  final String? note;
  final List<ExamEvent> events;

  ExamEvent? event(String code) {
    for (final e in events) {
      if (e.code == code) {
        return e;
      }
    }
    return null;
  }

  Map<String, Object?> toJson() => {
    'examYear': examYear,
    'sourceUrl': sourceUrl,
    'note': note,
    'events': [for (final e in events) e.toJson()],
  };
}

/// The published calendar; [version] doubles as the ETag.
class ExamSchedule {
  const ExamSchedule({required this.version, required this.years});

  factory ExamSchedule.fromJson(Map<String, dynamic> json) => ExamSchedule(
    version: json['version'] as String,
    years: [
      for (final y in json['calendars'] as List<dynamic>)
        ExamYear.fromJson(y as Map<String, dynamic>),
    ],
  );

  final String version;
  final List<ExamYear> years;

  ExamYear? year(int examYear) {
    for (final y in years) {
      if (y.examYear == examYear) {
        return y;
      }
    }
    return null;
  }

  Map<String, Object?> toJson() => {
    'version': version,
    'calendars': [for (final y in years) y.toJson()],
  };
}

/// Whole days left until a sitting, counted on Türkiye calendar days (the exam day itself is 0).
abstract final class Countdown {
  static const _turkey = Duration(hours: 3);

  static int daysUntil(DateTime eventStart, DateTime now) {
    DateTime day(DateTime t) {
      final local = t.toUtc().add(_turkey);
      return DateTime.utc(local.year, local.month, local.day);
    }

    return day(eventStart).difference(day(now)).inDays;
  }
}
