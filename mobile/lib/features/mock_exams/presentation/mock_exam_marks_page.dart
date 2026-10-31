import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design/design.dart';
import '../../curriculum/application/curriculum_providers.dart';
import '../../curriculum/domain/curriculum.dart';
import '../application/mock_exam_providers.dart';
import '../domain/mock_exam.dart';
import 'mock_exam_format.dart';

/// Puts a mock exam's wrong and blank answers on topics (M11c). Only fully marked sections count in the
/// analysis, so the page shows what is left per section.
class MockExamMarksPage extends ConsumerStatefulWidget {
  const MockExamMarksPage({super.key, required this.id});

  static const pattern = '/mock-exams/:id/marks';

  static String pathFor(String id) => '/mock-exams/$id/marks';

  final String id;

  @override
  ConsumerState<MockExamMarksPage> createState() => _MockExamMarksPageState();
}

class _MockExamMarksPageState extends ConsumerState<MockExamMarksPage> {
  MockExam? _exam;
  final _marks = <_MarkDraft>[];
  var _loading = true;
  var _saving = false;
  var _errors = const <String>[];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final exam = await ref.read(mockExamRepositoryProvider).get(widget.id);
    if (!mounted) {
      return;
    }
    setState(() {
      _exam = exam;
      _marks.addAll((exam?.marks ?? const []).map(_MarkDraft.from));
      _loading = false;
    });
  }

  List<_MarkDraft> _of(String section) => [
    for (final m in _marks)
      if (m.sectionCode == section) m,
  ];

  Future<void> _addTopic(MockSectionResult section, Curriculum c) async {
    final def = c.mockSections
        .where((s) => s.code == section.sectionCode)
        .firstOrNull;
    final subjects = def == null ? const <Subject>[] : c.subjectsOfSection(def);
    final taken = {for (final m in _of(section.sectionCode)) m.topicId};
    final topicId = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (context) => _TopicPicker(subjects: subjects, exclude: taken),
    );
    if (topicId == null) {
      return;
    }
    final marks = _of(section.sectionCode);
    final wrongLeft = section.wrong - marks.fold(0, (s, m) => s + m.wrong);
    setState(
      () => _marks.add(
        _MarkDraft.added(
          section.sectionCode,
          topicId,
          wrong: wrongLeft > 0 ? 1 : 0,
        ),
      ),
    );
  }

  Future<void> _save(Curriculum c) async {
    final exam = _exam!.copyWith(
      marks: [
        for (final m in _marks)
          if (m.wrong + m.blank > 0) m.toMark(),
      ],
    );
    final errors = MockExamRules.validate(exam, c, DateTime.now());
    setState(() => _errors = errors);
    if (errors.isNotEmpty) {
      return;
    }
    setState(() => _saving = true);
    await ref.read(mockExamRepositoryProvider).save(exam);
    if (mounted) {
      context.pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final curriculum = ref.watch(curriculumProvider).value;
    final exam = _exam;
    return Scaffold(
      appBar: AppBar(title: const Text('Yanlışları konulara ayır')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : exam == null
          ? const Center(child: Text('Bu deneme bulunamadı.'))
          : curriculum == null
          ? const Center(child: Text('Konu listesi henüz yüklenmedi.'))
          : _buildBody(context, exam, curriculum),
    );
  }

  Widget _buildBody(BuildContext context, MockExam exam, Curriculum c) {
    final textTheme = Theme.of(context).textTheme;
    final topics = c.topicsById;
    final sections = orderedSections(exam, c).where((s) => s.lost > 0).toList();
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.lg),
      children: [
        Text(
          'Her bölümde kaçırdığın soruları konulara dağıt. Bir bölümün tamamını dağıtırsan analiz o bölümü '
          'kullanır; yarım kalan bölüm hesaba katılmaz.',
          style: textTheme.bodySmall,
        ),
        const SizedBox(height: AppSpacing.md),
        if (sections.isEmpty) const Text('Bu denemede yanlış ya da boş yok.'),
        for (final section in sections)
          _sectionCard(context, section, c, topics),
        if (_errors.isNotEmpty)
          for (final e in _errors)
            Text(
              e,
              style: textTheme.bodyMedium?.copyWith(
                color: Theme.of(context).colorScheme.error,
              ),
            ),
        const SizedBox(height: AppSpacing.lg),
        NButton(
          label: 'Kaydet',
          expand: true,
          onPressed: _saving ? null : () => _save(c),
        ),
      ],
    );
  }

  Widget _sectionCard(
    BuildContext context,
    MockSectionResult section,
    Curriculum c,
    Map<String, Topic> topics,
  ) {
    final textTheme = Theme.of(context).textTheme;
    final marks = _of(section.sectionCode);
    final wrong = marks.fold(0, (s, m) => s + m.wrong);
    final blank = marks.fold(0, (s, m) => s + m.blank);
    final full = wrong + blank == section.lost;
    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.md),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    sectionLabel(section.sectionCode, c),
                    style: textTheme.titleSmall,
                  ),
                ),
                NBadge(
                  taggingLabel(
                    full
                        ? TaggingStatus.full
                        : wrong + blank == 0
                        ? TaggingStatus.none
                        : TaggingStatus.partial,
                  ),
                  tone: full ? NBadgeTone.success : NBadgeTone.neutral,
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'Yanlış $wrong/${section.wrong} · Boş $blank/${section.blank}',
              style: textTheme.bodySmall,
            ),
            for (final m in marks)
              Padding(
                padding: const EdgeInsets.only(top: AppSpacing.sm),
                child: Row(
                  children: [
                    Expanded(
                      child: Text(
                        topics[m.topicId]?.name ?? 'Kaldırılmış konu',
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    _Stepper(
                      label: 'Y',
                      value: m.wrong,
                      canAdd: wrong < section.wrong,
                      onChanged: (v) => setState(() => m.wrong = v),
                    ),
                    _Stepper(
                      label: 'B',
                      value: m.blank,
                      canAdd: blank < section.blank,
                      onChanged: (v) => setState(() => m.blank = v),
                    ),
                    IconButton(
                      tooltip: 'Konuyu çıkar',
                      icon: const Icon(Icons.close),
                      onPressed: () => setState(() => _marks.remove(m)),
                    ),
                  ],
                ),
              ),
            if (!full)
              TextButton.icon(
                onPressed: () => _addTopic(section, c),
                icon: const Icon(Icons.add),
                label: const Text('Konu ekle'),
              ),
          ],
        ),
      ),
    );
  }
}

class _MarkDraft {
  _MarkDraft({
    required this.sectionCode,
    required this.topicId,
    required this.wrong,
    required this.blank,
    required this.knowledge,
    required this.attention,
    required this.time,
  });

  _MarkDraft.added(this.sectionCode, this.topicId, {required this.wrong})
    : blank = wrong == 0 ? 1 : 0,
      knowledge = null,
      attention = null,
      time = null;

  _MarkDraft.from(TopicMark m)
    : this(
        sectionCode: m.sectionCode,
        topicId: m.topicId,
        wrong: m.wrong,
        blank: m.blank,
        knowledge: m.mistakeKnowledge,
        attention: m.mistakeAttention,
        time: m.mistakeTime,
      );

  final String sectionCode;
  final String topicId;
  int wrong;
  int blank;
  final int? knowledge;
  final int? attention;
  final int? time;

  /// Reasons set elsewhere are kept while they still fit the wrong count.
  TopicMark toMark() {
    final keep = (knowledge ?? 0) + (attention ?? 0) + (time ?? 0) <= wrong;
    return TopicMark(
      sectionCode: sectionCode,
      topicId: topicId,
      wrong: wrong,
      blank: blank,
      mistakeKnowledge: keep ? knowledge : null,
      mistakeAttention: keep ? attention : null,
      mistakeTime: keep ? time : null,
    );
  }
}

class _Stepper extends StatelessWidget {
  const _Stepper({
    required this.label,
    required this.value,
    required this.canAdd,
    required this.onChanged,
  });

  final String label;
  final int value;
  final bool canAdd;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      IconButton(
        tooltip: '$label azalt',
        visualDensity: VisualDensity.compact,
        icon: const Icon(Icons.remove),
        onPressed: value > 0 ? () => onChanged(value - 1) : null,
      ),
      Text('$label $value'),
      IconButton(
        tooltip: '$label artır',
        visualDensity: VisualDensity.compact,
        icon: const Icon(Icons.add),
        onPressed: canAdd ? () => onChanged(value + 1) : null,
      ),
    ],
  );
}

/// Topics of the section's subjects, searchable; returns the picked topic id.
class _TopicPicker extends StatefulWidget {
  const _TopicPicker({required this.subjects, required this.exclude});

  final List<Subject> subjects;
  final Set<String> exclude;

  @override
  State<_TopicPicker> createState() => _TopicPickerState();
}

class _TopicPickerState extends State<_TopicPicker> {
  var _query = '';

  static String _normalize(String text) => text
      .replaceAll('İ', 'i')
      .replaceAll('I', 'ı')
      .toLowerCase()
      .replaceAll('ı', 'i')
      .trim();

  @override
  Widget build(BuildContext context) {
    final entries = [
      for (final s in widget.subjects)
        for (final t in s.topics)
          if (!widget.exclude.contains(t.id) &&
              (_query.isEmpty ||
                  _normalize('${s.name} ${t.name}').contains(_query)))
            (subject: s, topic: t),
    ];
    return SizedBox(
      height: MediaQuery.sizeOf(context).height * 0.8,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg),
            child: TextField(
              autofocus: true,
              onChanged: (v) => setState(() => _query = _normalize(v)),
              decoration: const InputDecoration(
                prefixIcon: Icon(Icons.search),
                hintText: 'Konu ara',
              ),
            ),
          ),
          Expanded(
            child: ListView.builder(
              itemCount: entries.length,
              itemBuilder: (context, i) => ListTile(
                title: Text(entries[i].topic.name),
                subtitle: Text(entries[i].subject.name),
                onTap: () => Navigator.pop(context, entries[i].topic.id),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
