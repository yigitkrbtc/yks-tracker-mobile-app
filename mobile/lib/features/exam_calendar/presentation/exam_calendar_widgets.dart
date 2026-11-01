import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design/design.dart';
import '../application/exam_schedule_providers.dart';
import '../domain/exam_schedule.dart';

/// Days left until the target year's TYT; "tahmini" until ÖSYM announces the date.
class CountdownCard extends ConsumerWidget {
  const CountdownCard({super.key, required this.now, this.onOpenCalendar});

  final DateTime Function() now;
  final VoidCallback? onOpenCalendar;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final year = ref.watch(targetExamYearProvider);
    final tyt = year?.event('tyt');
    if (year == null || tyt == null) {
      return const SizedBox.shrink();
    }
    final days = Countdown.daysUntil(tyt.startsAt, now());
    final textTheme = Theme.of(context).textTheme;
    return NCard(
      title: 'YKS ${year.examYear}',
      description: days > 0
          ? 'TYT ${formatDay(tyt.startsAt.add(const Duration(hours: 3)))}'
          : 'Sınav günü geçti; hedef yılını profilinden güncelleyebilirsin.',
      trailing: tyt.estimated
          ? const NBadge('Tahmini', tone: NBadgeTone.warning)
          : null,
      footer: onOpenCalendar == null
          ? null
          : Align(
              alignment: Alignment.centerLeft,
              child: TextButton(
                onPressed: onOpenCalendar,
                child: const Text('Takvimi gör'),
              ),
            ),
      child: days > 0
          ? Text(
              '$days gün kaldı',
              key: const Key('countdown-days'),
              style: textTheme.headlineSmall,
            )
          : null,
    );
  }
}

/// Every event of the target year, with the source and the estimate note.
class ExamCalendarPage extends ConsumerWidget {
  const ExamCalendarPage({super.key});

  static const path = '/exam-calendar';

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final schedule = ref.watch(examScheduleProvider);
    final textTheme = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Sınav takvimi')),
      body: switch (schedule) {
        AsyncData(:final value) => ListView(
          padding: const EdgeInsets.all(AppSpacing.xl),
          children: [
            for (final year in value.years) ...[
              Text('YKS ${year.examYear}', style: textTheme.titleMedium),
              if (year.note case final note?) ...[
                const SizedBox(height: AppSpacing.xs),
                Text(note, style: textTheme.bodySmall),
              ],
              for (final e in year.events)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(e.name),
                  subtitle: Text(_when(e)),
                  trailing: e.estimated
                      ? const NBadge('Tahmini', tone: NBadgeTone.warning)
                      : null,
                ),
              Text('Kaynak: ${year.sourceUrl}', style: textTheme.bodySmall),
              const SizedBox(height: AppSpacing.xl),
            ],
          ],
        ),
        AsyncError() => const Center(
          child: Padding(
            padding: EdgeInsets.all(AppSpacing.xl),
            child: Text(
              'Sınav takvimi yüklenemedi; internete bağlanınca tekrar dene.',
            ),
          ),
        ),
        _ => const Center(child: CircularProgressIndicator()),
      },
    );
  }

  static String _when(ExamEvent e) {
    String at(DateTime t) {
      final local = t.add(const Duration(hours: 3));
      final hh = local.hour.toString().padLeft(2, '0');
      final mm = local.minute.toString().padLeft(2, '0');
      return '${formatDay(local)}, $hh:$mm';
    }

    final end = e.endsAt;
    return end == null
        ? at(e.startsAt)
        : '${formatDay(e.startsAt.add(const Duration(hours: 3)))} – ${formatDay(end.add(const Duration(hours: 3)))}';
  }
}
