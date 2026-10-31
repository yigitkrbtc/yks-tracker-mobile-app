import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design/design.dart';
import '../../../core/sync/sync_providers.dart';
import '../../../core/sync/sync_service.dart';
import '../../curriculum/application/curriculum_providers.dart';
import '../../curriculum/domain/curriculum.dart';
import '../application/analysis_providers.dart';
import '../domain/analysis.dart';
import '../domain/analysis_repository.dart';
import 'analysis_format.dart';
import 'topic_analysis_page.dart';

/// The analysis panel (S5): what to work on next, test and subject mastery, mock exam form. Every number comes
/// from the server; pull to send waiting records first, then fetch the recomputed panel.
class AnalysisView extends ConsumerWidget {
  const AnalysisView({super.key});

  Future<void> _refresh(BuildContext context, WidgetRef ref) async {
    final outcome = await ref.read(syncSchedulerProvider).syncNow();
    if (outcome == SyncOutcome.offline && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Sunucuya ulaşılamadı; son indirilen analiz gösteriliyor.',
          ),
        ),
      );
    }
    await ref.read(analysisOverviewProvider.notifier).refresh();
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final overview = ref.watch(analysisOverviewProvider);
    final curriculum = ref.watch(curriculumProvider).value;
    final queued = ref.watch(queuedWriteCountProvider).value ?? 0;

    return switch (overview) {
      AsyncData(:final value?) => RefreshIndicator(
        onRefresh: () => _refresh(context, ref),
        child: ListView(
          padding: const EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.md,
            AppSpacing.lg,
            AppSpacing.xxl,
          ),
          children: [
            if (queued > 0) ...[
              _SyncNotice(queued: queued),
              const SizedBox(height: AppSpacing.md),
            ],
            Text(
              'Son hesaplama: ${_stamp(value.computedAt)}',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            const SizedBox(height: AppSpacing.md),
            for (final card in _cards(context, value, curriculum)) ...[
              card,
              const SizedBox(height: AppSpacing.md),
            ],
          ],
        ),
      ),
      AsyncError(:final error) => _Message(
        text: error is AnalysisException
            ? error.message
            : 'Analiz yüklenemedi; tekrar dene.',
        onRetry: () => ref.invalidate(analysisOverviewProvider),
      ),
      AsyncData() => const SizedBox.shrink(),
      _ => const Center(child: CircularProgressIndicator()),
    };
  }

  List<Widget> _cards(
    BuildContext context,
    AnalysisOverview o,
    Curriculum? curriculum,
  ) {
    final topics = curriculum?.topicsById ?? const <String, Topic>{};
    void open(String id) => context.push(TopicAnalysisPage.pathFor(id));

    final tests = o.tests.where((t) => t.isActive && t.studiedTopics > 0);
    final subjects = o.subjects.where((s) => s.isActive && s.studiedTopics > 0);
    return [
      if (!o.hasData)
        const NCard(
          title: 'Analiz için henüz kayıt yok',
          description:
              'Konu testi ve deneme ekledikçe hangi konuya odaklanman gerektiğini, '
              'ne kadar emin olduğumuzla birlikte burada gösteririz.',
        ),
      if (o.priorities.isNotEmpty)
        NCard(
          key: const ValueKey('priorities'),
          title: 'Sıradaki odak',
          description: 'Sınavda sık çıkan ve ustalığın hedefin altında kalan konular; en çok net kazandıracaklar üstte.',
          child: Column(
            children: [
              for (final p in o.priorities)
                _TopicRow(
                  name: topics[p.topicId]?.name ?? 'Konu',
                  detail:
                      'Ustalık ${percent(p.mastery)} · sınavda ≈${decimal(p.expectedQuestions)} soru · '
                      'kazanç ≈${decimal(p.gain)} net'
                      '${p.mockBoost > 0 ? ' · denemelerde de kaçıyor' : ''}',
                  onTap: () => open(p.topicId),
                ),
            ],
          ),
        ),
      if (o.lowDataTopics.isNotEmpty)
        NCard(
          key: const ValueKey('low-data'),
          title: 'Veri az, ağırlığı yüksek',
          description: 'Bu konular hakkında karar verecek kadar kayıt yok; birkaç test çözersen netleşir.',
          child: Column(
            children: [
              for (final id in o.lowDataTopics)
                _TopicRow(
                  name: topics[id]?.name ?? 'Konu',
                  detail: topics[id] == null
                      ? ''
                      : 'Sınavda ≈${decimal(topics[id]!.expectedQuestions)} soru',
                  onTap: () => open(id),
                ),
            ],
          ),
        ),
      if (tests.isNotEmpty)
        NCard(
          key: const ValueKey('tests'),
          title: 'Testler',
          description:
              'Konu testlerinden, konuların sınavdaki ağırlığıyla hesaplanır.',
          child: Column(
            children: [
              for (final t in tests)
                _AggregateRow(name: _testName(t.code, curriculum), status: t),
            ],
          ),
        ),
      if (subjects.isNotEmpty)
        NCard(
          key: const ValueKey('subjects'),
          title: 'Dersler',
          child: Column(
            children: [
              for (final s in subjects)
                _AggregateRow(
                  name: _subjectName(s.code, curriculum),
                  status: s,
                ),
            ],
          ),
        ),
      if (o.sections.isNotEmpty)
        NCard(
          key: const ValueKey('sections'),
          title: 'Deneme formu',
          description: 'Denemelerdeki doğruluk; yakın tarihli denemeler daha çok sayılır.',
          child: Column(
            children: [
              for (final s in o.sections)
                _SectionRow(name: sectionName(s.code, curriculum), form: s),
            ],
          ),
        ),
    ];
  }

  static String _stamp(DateTime at) {
    final local = at.toLocal();
    String two(int v) => v.toString().padLeft(2, '0');
    return '${formatDay(local)} ${two(local.hour)}:${two(local.minute)}';
  }

  /// "TYT Türkçe": a test's root section carries the test's code and name.
  static String _testName(String code, Curriculum? curriculum) {
    final name = sectionName(code, curriculum);
    return '${code.split('-').first} $name';
  }

  static String _subjectName(String code, Curriculum? curriculum) {
    for (final s in curriculum?.subjects ?? const <Subject>[]) {
      if (s.code == code) {
        return '${code.split('.').first} ${s.name}';
      }
    }
    return code;
  }
}

class _SyncNotice extends StatelessWidget {
  const _SyncNotice({required this.queued});

  final int queued;

  @override
  Widget build(BuildContext context) {
    return NCard(
      key: const ValueKey('sync-notice'),
      title: '$queued kayıt henüz gönderilmedi',
      description:
          'Analiz sunucuda hesaplanır; bu kayıtlar gönderilince panele girer.',
    );
  }
}

class _TopicRow extends StatelessWidget {
  const _TopicRow({
    required this.name,
    required this.detail,
    required this.onTap,
  });

  final String name;
  final String detail;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    // Its own Material: the card paints a background that would hide the ink.
    return Material(
      type: MaterialType.transparency,
      child: InkWell(
        borderRadius: AppRadius.sm,
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(name, style: text.titleSmall),
                    if (detail.isNotEmpty) Text(detail, style: text.bodySmall),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right),
            ],
          ),
        ),
      ),
    );
  }
}

class _AggregateRow extends StatelessWidget {
  const _AggregateRow({required this.name, required this.status});

  final String name;
  final AggregateStatus status;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(name, style: text.titleSmall)),
              Text(percent(status.mastery), style: text.titleSmall),
            ],
          ),
          const SizedBox(height: AppSpacing.xs),
          RangeBar(
            value: status.mastery,
            low: status.masteryLow,
            high: status.masteryHigh,
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            'Muhtemelen ${percentRange(status.masteryLow, status.masteryHigh)} · '
            '${status.studiedTopics}/${status.topicCount} konu · '
            'sınav ağırlığının ${percent(status.coverage)} kadarı yeterli veriyle',
            style: text.bodySmall,
          ),
        ],
      ),
    );
  }
}

class _SectionRow extends StatelessWidget {
  const _SectionRow({required this.name, required this.form});

  final String name;
  final SectionForm form;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final missing = 3 - form.exams;
    final gap = form.practiceGap;
    return Padding(
      key: ValueKey('section-${form.code}'),
      padding: const EdgeInsets.only(bottom: AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(name, style: text.titleSmall)),
              if (form.isSufficient &&
                  form.trend.label != TrendLabel.insufficient)
                NBadge(
                  trendLabel(form.trend.label),
                  tone: trendTone(form.trend.label),
                ),
            ],
          ),
          const SizedBox(height: AppSpacing.xs),
          if (!form.isSufficient)
            Text(
              '${form.exams} deneme · güvenilir bir form için $missing deneme daha gerekiyor',
              style: text.bodySmall,
            )
          else ...[
            RangeBar(
              value: form.accuracy,
              low: form.accuracyLow,
              high: form.accuracyHigh,
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'Doğruluk ${percent(form.accuracy)} (${percentRange(form.accuracyLow, form.accuracyHigh)}) · '
              'beklenen net ≈${decimal(form.expectedNet)} '
              '(${decimal(form.expectedNetLow)}–${decimal(form.expectedNetHigh)}) · ${form.exams} deneme',
              style: text.bodySmall,
            ),
          ],
          if (form.gapWarning && gap != null) ...[
            const SizedBox(height: AppSpacing.xs),
            Text(
              'Konu testlerinde ${percent(form.accuracy + gap)}, denemede ${percent(form.accuracy)}. '
              'Süreli ve karışık soru pratiği bu farkı kapatmaya yardım edebilir.',
              style: text.bodySmall,
            ),
          ],
        ],
      ),
    );
  }
}

class _Message extends StatelessWidget {
  const _Message({required this.text, required this.onRetry});

  final String text;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(text, textAlign: TextAlign.center),
            const SizedBox(height: AppSpacing.lg),
            NButton(
              label: 'Tekrar dene',
              variant: NButtonVariant.outline,
              onPressed: onRetry,
            ),
          ],
        ),
      ),
    );
  }
}
