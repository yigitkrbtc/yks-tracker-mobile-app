import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design/design.dart';
import '../../../core/sync/sync_providers.dart';
import '../../../core/sync/sync_service.dart';
import '../../curriculum/application/curriculum_providers.dart';
import '../../curriculum/domain/curriculum.dart';
import '../application/topic_test_providers.dart';
import '../domain/topic_test.dart';
import 'topic_test_form_page.dart';
import 'topic_test_format.dart';

/// The student's topic tests, newest first. Pull to send waiting records and fetch other devices' changes.
class TopicTestListView extends ConsumerWidget {
  const TopicTestListView({super.key});

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
    final tests = ref.watch(topicTestsProvider);
    final curriculum = ref.watch(curriculumProvider).value;
    return Scaffold(
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push(TopicTestFormPage.newPath),
        icon: const Icon(Icons.add),
        label: const Text('Test ekle'),
      ),
      body: switch (tests) {
        AsyncData(:final value) => RefreshIndicator(
          onRefresh: () => _refresh(context, ref),
          child: value.isEmpty
              ? ListView(
                  children: const [
                    Padding(
                      padding: EdgeInsets.all(AppSpacing.xl),
                      child: Text(
                        'Henüz konu testi yok. Çözdüğün testi ekle; internet olmasa da kaydedilir.',
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
                      _TestTile(test: value[i], curriculum: curriculum),
                ),
        ),
        AsyncError() => const Center(child: Text('Testler okunamadı.')),
        _ => const Center(child: CircularProgressIndicator()),
      },
    );
  }
}

class _TestTile extends StatelessWidget {
  const _TestTile({required this.test, required this.curriculum});

  final TopicTest test;
  final Curriculum? curriculum;

  @override
  Widget build(BuildContext context) {
    final topics = curriculum?.topicsById ?? const <String, Topic>{};
    final names = test.results.map((r) => topicName(r.topicId, topics));
    final textTheme = Theme.of(context).textTheme;
    return Card(
      margin: EdgeInsets.zero,
      child: InkWell(
        borderRadius: AppRadius.md,
        onTap: () => context.push(TopicTestFormPage.pathFor(test.id)),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      '${formatDay(test.studiedOn)} · ${sourceLabel(test, curriculum)}',
                      style: textTheme.labelMedium,
                    ),
                  ),
                  if (test.isRepeat) const NBadge('Tekrar'),
                ],
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                names.join(', '),
                style: textTheme.titleSmall,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                scoreLine(test.correct, test.wrong, test.blank, test.net),
                style: textTheme.bodyMedium,
              ),
              if (test.syncStatus != SyncStatus.synced) ...[
                const SizedBox(height: AppSpacing.sm),
                SyncBadge(status: test.syncStatus),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
