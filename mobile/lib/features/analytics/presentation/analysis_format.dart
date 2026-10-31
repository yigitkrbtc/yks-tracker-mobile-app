import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design/design.dart';
import '../../curriculum/domain/curriculum.dart';
import '../application/analysis_providers.dart';
import '../domain/analysis.dart';

/// "%65" (Turkish puts the sign first).
String percent(double value) => '%${(value * 100).round()}';

/// "%47–%81".
String percentRange(double low, double high) =>
    '${percent(low)}–${percent(high)}';

/// One decimal with a comma: 3,4.
String decimal(double value) => value.toStringAsFixed(1).replaceAll('.', ',');

String confidenceLabel(ConfidenceLevel level) => switch (level) {
  ConfidenceLevel.none => 'Kayıt yok',
  ConfidenceLevel.veryLow => 'Çok az veri',
  ConfidenceLevel.preliminary => 'Ön bulgu',
  ConfidenceLevel.reasonable => 'Makul',
  ConfidenceLevel.reliable => 'Güvenilir',
};

String bandLabel(MasteryBand band) => switch (band) {
  MasteryBand.none => 'Kayıt yok',
  MasteryBand.insufficient => 'Henüz erken',
  MasteryBand.weak => 'Zayıf',
  MasteryBand.developing => 'Gelişiyor',
  MasteryBand.strong => 'Güçlü',
};

NBadgeTone bandTone(MasteryBand band) => switch (band) {
  MasteryBand.weak => NBadgeTone.danger,
  MasteryBand.developing => NBadgeTone.warning,
  MasteryBand.strong => NBadgeTone.success,
  _ => NBadgeTone.neutral,
};

String trendLabel(TrendLabel trend) => switch (trend) {
  TrendLabel.none || TrendLabel.insufficient => 'Trend için veri az',
  TrendLabel.improving => 'Yükseliyor',
  TrendLabel.stable => 'Kararlı',
  TrendLabel.declining => 'Düşüyor',
};

NBadgeTone trendTone(TrendLabel trend) => switch (trend) {
  TrendLabel.improving => NBadgeTone.success,
  TrendLabel.declining => NBadgeTone.warning,
  _ => NBadgeTone.neutral,
};

/// Plain sentence behind a topic trend (M3), self-referenced and without blame.
String topicTrendSentence(Trend trend) => switch (trend.label) {
  TrendLabel.none => 'Bu konuda henüz kayıt yok.',
  TrendLabel.insufficient => 'Trend için son 3 haftada en az 2 test ve toplam 4 kayıt, 60 soru gerekiyor.',
  TrendLabel.improving =>
    'Son 3 haftadaki doğruluğun önceki 2 aya göre belirgin yüksek '
        '(olasılık ${percent(trend.probabilityUp ?? 0)}).',
  TrendLabel.stable =>
    'Son 3 haftadaki doğruluğun önceki 2 aydan belirgin farklı değil.',
  TrendLabel.declining =>
    'Son 3 haftadaki doğruluğun önceki 2 aya göre düşük '
        '(olasılık ${percent(1 - (trend.probabilityUp ?? 1))}). Kısa bir tekrar iyi gelebilir.',
};

/// Section name with its block when it is a subject line: "Sosyal Bilimler · Tarih".
String sectionName(String code, Curriculum? curriculum) {
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

/// A value with its interval on a 0–1 track: the band is the interval, the tick the estimate.
class RangeBar extends StatelessWidget {
  const RangeBar({
    super.key,
    required this.value,
    required this.low,
    required this.high,
    this.height = 10,
  });

  final double value;
  final double low;
  final double high;
  final double height;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Semantics(
      label: '${percent(value)}, muhtemelen ${percentRange(low, high)}',
      child: LayoutBuilder(
        builder: (context, constraints) {
          final w = constraints.maxWidth;
          double x(double v) => v.clamp(0.0, 1.0) * w;
          return SizedBox(
            height: height,
            child: Stack(
              children: [
                Container(
                  decoration: BoxDecoration(
                    color: scheme.surfaceContainerHighest,
                    borderRadius: AppRadius.pill,
                  ),
                ),
                Positioned(
                  left: x(low),
                  width: (x(high) - x(low)).clamp(2.0, w),
                  top: 0,
                  bottom: 0,
                  child: Container(
                    decoration: BoxDecoration(
                      color: scheme.primary.withValues(alpha: 0.35),
                      borderRadius: AppRadius.pill,
                    ),
                  ),
                ),
                Positioned(
                  left: (x(value) - 1.5).clamp(0.0, w - 3),
                  width: 3,
                  top: 0,
                  bottom: 0,
                  child: ColoredBox(color: scheme.primary),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

/// Small status dot for topic lists: grey without data, then by band. Wired in by the app shell.
class TopicStatusDot extends ConsumerWidget {
  const TopicStatusDot({super.key, required this.topicId});

  final String topicId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final status = ref.watch(topicStatusProvider(topicId));
    final scheme = Theme.of(context).colorScheme;
    final band = status?.band ?? MasteryBand.none;
    final color = switch (band) {
      MasteryBand.weak => scheme.error,
      MasteryBand.developing => const Color(0xFFC98A12),
      MasteryBand.strong => const Color(0xFF2E8B57),
      MasteryBand.insufficient => scheme.outline,
      MasteryBand.none => scheme.outlineVariant,
    };
    final label = status?.mastery == null
        ? bandLabel(band)
        : '${bandLabel(band)} · ${percent(status!.mastery!)}';
    return Tooltip(
      message: label,
      child: Semantics(
        label: label,
        child: Container(
          key: ValueKey('status-$topicId'),
          width: 12,
          height: 12,
          decoration: BoxDecoration(
            color: band == MasteryBand.none ? null : color,
            border: Border.all(color: color, width: 2),
            shape: BoxShape.circle,
          ),
        ),
      ),
    );
  }
}
