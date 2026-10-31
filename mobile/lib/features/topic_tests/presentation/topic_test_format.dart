import 'package:flutter/widgets.dart';

import '../../../core/design/design.dart';
import '../../curriculum/domain/curriculum.dart';
import '../domain/topic_test.dart';

const _months = [
  'Ocak',
  'Şubat',
  'Mart',
  'Nisan',
  'Mayıs',
  'Haziran',
  'Temmuz',
  'Ağustos',
  'Eylül',
  'Ekim',
  'Kasım',
  'Aralık',
];

/// "31 Ekim 2026".
String formatDay(DateTime d) => '${d.day} ${_months[d.month - 1]} ${d.year}';

/// Net with a decimal comma and no trailing zeros: 11,25 / 11,5 / 11.
String formatNet(double net) => net
    .toStringAsFixed(2)
    .replaceAll(RegExp(r'\.?0+$'), '')
    .replaceAll('.', ',');

String scoreLine(int correct, int wrong, int blank, double net) =>
    'D $correct · Y $wrong · B $blank · Net ${formatNet(net)}';

/// The source as the student sees it: the listed name, or what they typed.
String sourceLabel(TopicTest t, Curriculum? curriculum) {
  final id = t.sourceId;
  if (id == null) {
    return t.sourceName ?? 'Kaynak yok';
  }
  for (final s in curriculum?.sources ?? const <QuestionSource>[]) {
    if (s.id == id) {
      return s.name;
    }
  }
  return 'Kaynak';
}

/// Topic name by id; a topic no longer in the curriculum keeps a neutral label.
String topicName(String id, Map<String, Topic> topics) =>
    topics[id]?.name ?? 'Kaldırılmış konu';

/// Shown only when the record is not on the server yet.
class SyncBadge extends StatelessWidget {
  const SyncBadge({super.key, required this.status});

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
