import 'package:drift/drift.dart';

import '../../../core/database/app_database.dart';

/// Outbox entity type of topic progress marks.
const topicProgressEntity = 'topic_progress';

abstract final class TopicProgressRecords {
  static Map<String, Object?> putPayload(String status, int micros) => {
    'status': status,
    'clientModifiedAt': DateTime.fromMicrosecondsSinceEpoch(
      micros,
      isUtc: true,
    ).toIso8601String(),
  };

  /// Stores the server's state of a mark (a push answer or a change feed entry).
  static Future<void> storeServer(AppDatabase db, Map<String, dynamic> json) =>
      db
          .into(db.topicProgressMarks)
          .insertOnConflictUpdate(
            TopicProgressMarksCompanion.insert(
              topicId: json['topicId'] as String,
              status: json['status'] as String,
              clientModifiedAt: DateTime.parse(
                json['clientModifiedAt'] as String,
              ).toUtc().microsecondsSinceEpoch,
              changeSeq: Value(json['changeSeq'] as int),
            ),
          );
}
