import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design/design.dart';
import '../../curriculum/application/curriculum_providers.dart';
import '../application/analysis_providers.dart';
import '../domain/analysis.dart';
import '../domain/analysis_repository.dart';
import 'analysis_format.dart';

/// The topic card (S5): mastery with its range and how sure we are, trend, mock exam misses, recent records.
class TopicAnalysisPage extends ConsumerWidget {
  const TopicAnalysisPage({
    super.key,
    required this.topicId,
    this.onRecordTest,
  });

  static const pattern = '/analysis/topics/:id';

  static String pathFor(String topicId) => '/analysis/topics/$topicId';

  final String topicId;

  /// Opens the test form for this topic; wired by the app, since another feature owns the form.
  final void Function(String topicId)? onRecordTest;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final topic = ref.watch(curriculumProvider).value?.topicsById[topicId];
    final analysis = ref.watch(topicAnalysisProvider(topicId));
    return Scaffold(
      appBar: AppBar(title: Text(topic?.name ?? 'Konu analizi')),
      body: switch (analysis) {
        AsyncData(:final value) => ListView(
          padding: const EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.md,
            AppSpacing.lg,
            AppSpacing.xxl,
          ),
          children: [
            for (final card in _cards(context, value)) ...[
              card,
              const SizedBox(height: AppSpacing.md),
            ],
            if (onRecordTest case final record?)
              NButton(
                label: 'Bu konuda test ekle',
                icon: Icons.add,
                expand: true,
                onPressed: () => record(topicId),
              ),
          ],
        ),
        AsyncError(:final error) => Center(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.xl),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  error is AnalysisException
                      ? error.message
                      : 'Konu analizi yüklenemedi.',
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: AppSpacing.lg),
                NButton(
                  label: 'Tekrar dene',
                  variant: NButtonVariant.outline,
                  onPressed: () =>
                      ref.invalidate(topicAnalysisProvider(topicId)),
                ),
              ],
            ),
          ),
        ),
        _ => const Center(child: CircularProgressIndicator()),
      },
    );
  }

  List<Widget> _cards(BuildContext context, TopicAnalysis a) {
    final text = Theme.of(context).textTheme;
    final next = _nextLevel(a.confidenceLevel);
    final nextHint = next == null || a.questionsToNextLevel == null
        ? null
        : '"${confidenceLabel(next)}" için yaklaşık ${a.questionsToNextLevel} yeni soru daha.';

    if (!a.hasData) {
      return [
        NCard(
          key: const ValueKey('no-data'),
          title: 'Bu konuda henüz kayıt yok',
          description:
              'İlk testini ekleyince ustalık tahmini burada görünür. ${nextHint ?? ''}'
                  .trim(),
        ),
      ];
    }

    final mastery = a.mastery!;
    final signal = a.mockSignal;
    return [
      NCard(
        key: const ValueKey('mastery'),
        title: 'Ustalık ${percent(mastery)}',
        description:
            'Muhtemelen ${percentRange(a.masteryLow!, a.masteryHigh!)} arasında (%90 olasılıkla).',
        trailing: NBadge(bandLabel(a.band), tone: bandTone(a.band)),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            RangeBar(value: mastery, low: a.masteryLow!, high: a.masteryHigh!),
            const SizedBox(height: AppSpacing.md),
            Text(
              'Güven: ${confidenceLabel(a.confidenceLevel)}',
              style: text.titleSmall,
            ),
            if (nextHint != null) Text(nextHint, style: text.bodySmall),
            if (a.band == MasteryBand.insufficient)
              Text(
                'Zayıf/güçlü demek için en az 20 soru ve 2 kayıt gerekiyor.',
                style: text.bodySmall,
              ),
            if (a.markingRate != null) ...[
              const SizedBox(height: AppSpacing.md),
              Text(
                'İşaretleme oranı ${percent(a.markingRate!)}'
                '${a.expectedNetPerQuestion == null ? '' : ' · soru başına ≈${decimal(a.expectedNetPerQuestion!)} net'}',
                style: text.bodySmall,
              ),
              if (a.optimistic)
                Text(
                  'Boş bıraktığın soru çok; doğruluk oranı seni olduğundan iyi gösteriyor olabilir.',
                  style: text.bodySmall,
                ),
            ],
          ],
        ),
      ),
      NCard(
        key: const ValueKey('trend'),
        title: 'Gidişat',
        trailing: NBadge(
          trendLabel(a.trend.label),
          tone: trendTone(a.trend.label),
        ),
        description: topicTrendSentence(a.trend),
      ),
      if (signal != null && signal.shown && signal.missed > 0)
        NCard(
          key: const ValueKey('mock-signal'),
          title: 'Denemelerde',
          description:
              'Son ${signal.exams} denemede bu konudan beklenen ≈${decimal(signal.exposure)} sorunun '
              '${signal.missed} tanesini kaçırdın (${signal.wrong} yanlış, ${signal.blank} boş). '
              'Dersin genelinde kaçırma oranın ${percent(signal.subjectRate)}, bu konuda ${percent(signal.rate)}.',
        ),
      NCard(
        key: const ValueKey('records'),
        title: 'Kayıtlar',
        description:
            '${a.records} kayıt · ${a.questions} soru · ${a.correct} doğru, ${a.wrong} yanlış, ${a.blank} boş',
        child: Column(
          children: [
            for (final r in a.recent)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: AppSpacing.xs),
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(formatDay(r.date), style: text.bodyMedium),
                          Text(
                            '${r.total} soru · ${r.correct}D ${r.wrong}Y ${r.blank}B'
                            '${r.isRepeat ? ' · tekrar' : ''}',
                            style: text.bodySmall,
                          ),
                        ],
                      ),
                    ),
                    Text('${formatNet(r.net)} net', style: text.bodyMedium),
                  ],
                ),
              ),
          ],
        ),
      ),
    ];
  }

  static ConfidenceLevel? _nextLevel(ConfidenceLevel level) => switch (level) {
    ConfidenceLevel.none ||
    ConfidenceLevel.veryLow => ConfidenceLevel.preliminary,
    ConfidenceLevel.preliminary => ConfidenceLevel.reasonable,
    ConfidenceLevel.reasonable => ConfidenceLevel.reliable,
    ConfidenceLevel.reliable => null,
  };
}
