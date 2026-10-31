import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design/design.dart';
import '../../profile/application/profile_providers.dart';
import '../../profile/domain/profile_repository.dart';
import '../application/curriculum_providers.dart';
import '../domain/curriculum.dart';
import '../domain/curriculum_repository.dart';

/// The student's topics: TYT and AYT tabs, one expandable section per subject, search by name.
class TopicListView extends ConsumerStatefulWidget {
  const TopicListView({super.key, this.onRecordTest});

  /// Opens the test form for a topic; the app shell wires it, since another feature owns the form.
  final void Function(Topic topic)? onRecordTest;

  @override
  ConsumerState<TopicListView> createState() => _TopicListViewState();
}

class _TopicListViewState extends ConsumerState<TopicListView> {
  final _search = TextEditingController();
  var _query = '';

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final subjects = ref.watch(visibleSubjectsProvider);
    return switch (subjects) {
      AsyncData(:final value) => _buildTabs(value),
      AsyncError(:final error) => _ErrorState(
        message: switch (error) {
          CurriculumException(:final message) => message,
          ProfileException(:final message) => message,
          _ => 'Konular yüklenemedi; tekrar dene.',
        },
        onRetry: () {
          if (ref.read(studyProfileProvider).hasError) {
            ref.invalidate(studyProfileProvider);
          }
          if (ref.read(curriculumProvider).hasError) {
            ref.invalidate(curriculumProvider);
          }
        },
      ),
      _ => const Center(child: CircularProgressIndicator()),
    };
  }

  Widget _buildTabs(List<Subject> subjects) {
    final sessions = [
      for (final s in ExamSession.values)
        if (subjects.any((subject) => subject.session == s)) s,
    ];
    return DefaultTabController(
      length: sessions.length,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(
              AppSpacing.lg,
              AppSpacing.md,
              AppSpacing.lg,
              0,
            ),
            child: TextField(
              controller: _search,
              onChanged: (v) => setState(() => _query = _normalize(v)),
              decoration: const InputDecoration(
                prefixIcon: Icon(Icons.search),
                hintText: 'Konu ara',
              ),
            ),
          ),
          if (sessions.length > 1)
            TabBar(
              tabs: [for (final s in sessions) Tab(text: _sessionLabel(s))],
            ),
          Expanded(
            child: TabBarView(
              children: [
                for (final s in sessions)
                  RefreshIndicator(
                    onRefresh: () =>
                        ref.read(curriculumProvider.notifier).refresh(),
                    child: _SubjectList(
                      subjects: subjects
                          .where((subject) => subject.session == s)
                          .toList(),
                      query: _query,
                      onRecordTest: widget.onRecordTest,
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static String _sessionLabel(ExamSession s) => switch (s) {
    ExamSession.tyt => 'TYT',
    ExamSession.ayt => 'AYT',
  };
}

/// Case and dotted/dotless i insensitive, so "integral" finds "İntegral".
String _normalize(String text) => text
    .replaceAll('İ', 'i')
    .replaceAll('I', 'ı')
    .toLowerCase()
    .replaceAll('ı', 'i')
    .trim();

class _SubjectList extends StatelessWidget {
  const _SubjectList({
    required this.subjects,
    required this.query,
    this.onRecordTest,
  });

  final List<Subject> subjects;
  final String query;
  final void Function(Topic topic)? onRecordTest;

  @override
  Widget build(BuildContext context) {
    final filtered = [
      for (final subject in subjects)
        (
          subject: subject,
          topics: query.isEmpty
              ? subject.topics
              : subject.topics
                    .where((t) => _normalize(t.name).contains(query))
                    .toList(),
        ),
    ].where((e) => e.topics.isNotEmpty).toList();

    if (filtered.isEmpty) {
      return ListView(
        children: const [
          Padding(
            padding: EdgeInsets.all(AppSpacing.xl),
            child: Center(child: Text('Aramana uyan konu yok.')),
          ),
        ],
      );
    }

    return ListView.builder(
      padding: const EdgeInsets.only(bottom: AppSpacing.xl),
      itemCount: filtered.length,
      itemBuilder: (context, i) {
        final entry = filtered[i];
        return ExpansionTile(
          // initiallyExpanded is read once; a new key per mode opens every match while searching.
          key: PageStorageKey('${entry.subject.code}|${query.isNotEmpty}'),
          initiallyExpanded: query.isNotEmpty,
          title: Text(entry.subject.name),
          subtitle: Text('${entry.topics.length} konu'),
          children: [
            for (final t in entry.topics)
              _TopicTile(topic: t, onRecordTest: onRecordTest),
          ],
        );
      },
    );
  }
}

class _TopicTile extends StatelessWidget {
  const _TopicTile({required this.topic, this.onRecordTest});

  final Topic topic;
  final void Function(Topic topic)? onRecordTest;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      title: Text(topic.name),
      subtitle: Text(_subtitle(topic)),
      trailing: TierBadge(topic: topic),
      onTap: () => showModalBottomSheet<void>(
        context: context,
        showDragHandle: true,
        builder: (context) =>
            _TopicSheet(topic: topic, onRecordTest: onRecordTest),
      ),
    );
  }

  static String _subtitle(Topic t) {
    final perExam = t.notAskedRecently
        ? 'son 5 yılda soru yok'
        : 'sınav başına ort. ${t.expectedQuestions.toStringAsFixed(1).replaceAll('.', ',')} soru';
    return '${t.gradeLabel} · $perExam';
  }
}

/// How often the topic is asked. A topic with no recent questions says so instead of a bare "rare".
class TierBadge extends StatelessWidget {
  const TierBadge({super.key, required this.topic});

  final Topic topic;

  @override
  Widget build(BuildContext context) {
    if (topic.notAskedRecently) {
      return const NBadge('Son yıllarda çıkmadı');
    }
    return switch (topic.tier) {
      WeightTier.high => const NBadge('Çok çıkar', tone: NBadgeTone.success),
      WeightTier.medium => const NBadge('Orta', tone: NBadgeTone.primary),
      WeightTier.low => const NBadge('Az'),
      WeightTier.rare => const NBadge('Nadir'),
    };
  }
}

class _TopicSheet extends StatelessWidget {
  const _TopicSheet({required this.topic, this.onRecordTest});

  final Topic topic;
  final void Function(Topic topic)? onRecordTest;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(
          AppSpacing.xl,
          0,
          AppSpacing.xl,
          AppSpacing.xl,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(topic.name, style: textTheme.titleMedium),
            const SizedBox(height: AppSpacing.sm),
            Wrap(
              spacing: AppSpacing.sm,
              children: [
                TierBadge(topic: topic),
                NBadge(topic.gradeLabel),
              ],
            ),
            const SizedBox(height: AppSpacing.lg),
            Text('Alt konular', style: textTheme.titleSmall),
            const SizedBox(height: AppSpacing.sm),
            for (final s in topic.subtopics)
              Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.xs),
                child: Text('• $s', style: textTheme.bodyMedium),
              ),
            const SizedBox(height: AppSpacing.md),
            Text(
              'Sıklık, 2019–2026 ÖSYM sorularını konulara ayırarak hesapladığımız '
              'tahmindir; resmî bir oran değildir.',
              style: textTheme.bodySmall,
            ),
            if (onRecordTest case final record?) ...[
              const SizedBox(height: AppSpacing.lg),
              NButton(
                label: 'Bu konuda test ekle',
                icon: Icons.add,
                expand: true,
                onPressed: () {
                  Navigator.pop(context);
                  record(topic);
                },
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(message, textAlign: TextAlign.center),
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
