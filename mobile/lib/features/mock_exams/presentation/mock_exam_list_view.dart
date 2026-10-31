import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design/design.dart';
import '../../../core/sync/sync_providers.dart';
import '../../../core/sync/sync_service.dart';
import '../../curriculum/application/curriculum_providers.dart';
import '../../curriculum/domain/curriculum.dart';
import '../application/mock_exam_providers.dart';
import '../domain/mock_exam.dart';
import 'mock_exam_form_page.dart';
import 'mock_exam_format.dart';

/// The student's mock exams, newest first. Pull to send waiting records and fetch other devices' changes.
class MockExamListView extends ConsumerWidget {
  const MockExamListView({super.key});

  Future<void> _refresh(BuildContext context, WidgetRef ref) async {
    final outcome = await ref.read(syncSchedulerProvider).syncNow();
    if (outcome == SyncOutcome.offline && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Sunucuya ulaşılamadı. Kayıtların bu cihazda; bağlantı gelince gönderilir.',
          ),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final exams = ref.watch(mockExamsProvider);
    final curriculum = ref.watch(curriculumProvider).value;
    return Scaffold(
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push(MockExamFormPage.newPath),
        icon: const Icon(Icons.add),
        label: const Text('Deneme ekle'),
      ),
      body: switch (exams) {
        AsyncData(:final value) => RefreshIndicator(
          onRefresh: () => _refresh(context, ref),
          child: value.isEmpty
              ? ListView(
                  children: const [
                    Padding(
                      padding: EdgeInsets.all(AppSpacing.xl),
                      child: Text(
                        'Henüz deneme yok. Çözdüğün denemenin netlerini ders ders gir; '
                        'internet olmasa da kaydedilir.',
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ],
                )
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(
                    AppSpacing.lg,
                    AppSpacing.md,
                    AppSpacing.lg,
                    96,
                  ),
                  itemCount: value.length,
                  separatorBuilder: (_, _) =>
                      const SizedBox(height: AppSpacing.sm),
                  itemBuilder: (context, i) =>
                      _ExamTile(exam: value[i], curriculum: curriculum),
                ),
        ),
        AsyncError() => const Center(child: Text('Denemeler okunamadı.')),
        _ => const Center(child: CircularProgressIndicator()),
      },
    );
  }
}

class _ExamTile extends StatelessWidget {
  const _ExamTile({required this.exam, required this.curriculum});

  final MockExam exam;
  final Curriculum? curriculum;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final sections = orderedSections(exam, curriculum);
    return Card(
      margin: EdgeInsets.zero,
      child: InkWell(
        borderRadius: AppRadius.md,
        onTap: () => context.push(MockExamFormPage.pathFor(exam.id)),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      '${formatDay(exam.takenOn)} · ${publisherLabel(exam, curriculum)}',
                      style: textTheme.labelMedium,
                    ),
                  ),
                  NBadge(exam.kind.label),
                ],
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                '${exam.title ?? '${exam.kind.label} denemesi'} · Net ${formatNet(exam.net)}',
                style: textTheme.titleSmall,
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                [
                  for (final s in sections)
                    '${sectionLabel(s.sectionCode, curriculum)} ${formatNet(s.net)}',
                ].join(' · '),
                style: textTheme.bodySmall,
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
              ),
              if (exam.needsTagging ||
                  exam.syncStatus != SyncStatus.synced) ...[
                const SizedBox(height: AppSpacing.sm),
                Wrap(
                  spacing: AppSpacing.sm,
                  runSpacing: AppSpacing.xs,
                  children: [
                    if (exam.needsTagging)
                      const NBadge('Yanlışlar etiketlenmedi'),
                    MockExamSyncBadge(status: exam.syncStatus),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
