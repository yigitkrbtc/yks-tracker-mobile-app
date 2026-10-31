import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design/design.dart';
import '../../curriculum/application/curriculum_providers.dart';
import '../../curriculum/domain/curriculum.dart';
import '../application/topic_test_providers.dart';
import '../domain/topic_test.dart';
import 'topic_test_format.dart';

/// Records a new topic test or edits one. Saving never waits for the network.
class TopicTestFormPage extends ConsumerStatefulWidget {
  const TopicTestFormPage({super.key, this.id, this.topicId, this.now});

  static const newPath = '/tests/new';
  static const editPattern = '/tests/:id';

  /// New test with [topicId] already added.
  static String newForTopic(String topicId) =>
      Uri(path: newPath, queryParameters: {'topic': topicId}).toString();

  static String pathFor(String id) => '/tests/$id';

  /// The test to edit; null for a new one.
  final String? id;

  /// Topic to start a new test with.
  final String? topicId;

  /// Fixed clock for tests.
  final DateTime? now;

  @override
  ConsumerState<TopicTestFormPage> createState() => _TopicTestFormPageState();
}

const _otherSource = '';

class _TopicTestFormPageState extends ConsumerState<TopicTestFormPage> {
  late final DateTime _now = widget.now ?? DateTime.now();
  late DateTime _date = TopicTestRules.todayInTurkey(_now);
  final _sourceName = TextEditingController();
  final _duration = TextEditingController();
  final _note = TextEditingController();
  final _rows = <_ResultDraft>[];
  String? _source; // a listed source id, or [_otherSource]
  var _isRepeat = false;
  Difficulty? _difficulty;
  TopicTest? _existing;
  var _loading = true;
  var _missing = false;
  var _saving = false;
  var _errors = const <String>[];

  bool get _editing => widget.id != null;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final repository = ref.read(topicTestRepositoryProvider);
    final id = widget.id;
    if (id != null) {
      final test = await repository.get(id);
      if (!mounted) {
        return;
      }
      if (test == null) {
        setState(() {
          _missing = true;
          _loading = false;
        });
        return;
      }
      _fill(test);
    } else {
      final last = await repository.lastSource();
      if (!mounted) {
        return;
      }
      if (last != null) {
        _source = last.id ?? _otherSource;
        _sourceName.text = last.id == null ? last.name ?? '' : '';
      }
      final topic = widget.topicId;
      if (topic != null) {
        _rows.add(_ResultDraft(topic));
      }
    }
    setState(() => _loading = false);
  }

  void _fill(TopicTest t) {
    _existing = t;
    _date = t.studiedOn;
    _source = t.sourceId ?? _otherSource;
    _sourceName.text = t.sourceId == null ? t.sourceName ?? '' : '';
    _isRepeat = t.isRepeat;
    _difficulty = t.difficulty;
    _duration.text = t.durationMinutes?.toString() ?? '';
    _note.text = t.note ?? '';
    _rows.addAll(t.results.map(_ResultDraft.from));
  }

  @override
  void dispose() {
    _sourceName.dispose();
    _duration.dispose();
    _note.dispose();
    for (final r in _rows) {
      r.dispose();
    }
    super.dispose();
  }

  TopicTest _build() {
    final other = _source == _otherSource;
    final note = _note.text.trim();
    // A preselected source that was retired is not shown, so it is not sent either; one the stored test already
    // has stays (the server keeps it).
    final listed = ref
        .read(curriculumProvider)
        .value
        ?.sources
        .map((s) => s.id)
        .toSet();
    final hidden =
        listed != null &&
        !listed.contains(_source) &&
        _source != _existing?.sourceId;
    return TopicTest(
      id: _existing?.id ?? ref.read(newTopicTestIdProvider)(),
      studiedOn: _date,
      sourceId: other || hidden ? null : _source,
      sourceName: other ? _sourceName.text.trim() : null,
      isRepeat: _isRepeat,
      difficulty: _difficulty,
      durationMinutes: int.tryParse(_duration.text),
      note: note.isEmpty ? null : note,
      results: [for (final r in _rows) r.toResult()],
      modifiedAt: _now,
    );
  }

  Future<void> _save() async {
    final test = _build();
    final errors = TopicTestRules.validate(test, widget.now ?? DateTime.now());
    setState(() => _errors = errors);
    if (errors.isNotEmpty) {
      return;
    }
    setState(() => _saving = true);
    await ref.read(topicTestRepositoryProvider).save(test);
    if (mounted) {
      context.pop();
    }
  }

  Future<void> _delete() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Test silinsin mi?'),
        content: const Text('Bu test tüm cihazlarından silinir.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Vazgeç'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Sil'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) {
      return;
    }
    await ref.read(topicTestRepositoryProvider).delete(widget.id!);
    if (mounted) {
      context.pop();
    }
  }

  Future<void> _pickDate() async {
    final today = TopicTestRules.todayInTurkey(_now);
    final first = TopicTestRules.earliestDate(_now);
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime(_date.year, _date.month, _date.day),
      firstDate: DateTime(first.year, first.month, first.day),
      lastDate: DateTime(today.year, today.month, today.day),
    );
    if (picked != null) {
      setState(
        () => _date = DateTime.utc(picked.year, picked.month, picked.day),
      );
    }
  }

  Future<void> _addTopic() async {
    final subjects = ref.read(visibleSubjectsProvider).value;
    final topicId = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (context) => _TopicPicker(
        subjects: subjects ?? const [],
        exclude: {for (final r in _rows) r.topicId},
      ),
    );
    if (topicId != null) {
      setState(() => _rows.add(_ResultDraft(topicId)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final curriculum = ref.watch(curriculumProvider).value;
    return Scaffold(
      appBar: AppBar(
        title: Text(_editing ? 'Testi düzenle' : 'Konu testi ekle'),
        actions: [
          if (_editing && !_missing)
            IconButton(
              tooltip: 'Sil',
              icon: const Icon(Icons.delete_outline),
              onPressed: _loading || _saving ? null : _delete,
            ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _missing
          ? const Center(child: Text('Bu test bulunamadı.'))
          : _buildForm(context, curriculum),
    );
  }

  Widget _buildForm(BuildContext context, Curriculum? curriculum) {
    final textTheme = Theme.of(context).textTheme;
    final topics = curriculum?.topicsById ?? const <String, Topic>{};
    final sources = curriculum?.sources ?? const <QuestionSource>[];
    final listed = {for (final s in sources) s.id};
    // A source no longer listed keeps the test editable as typed text.
    final source =
        _source == null || _source == _otherSource || listed.contains(_source)
        ? _source
        : null;
    final syncError = _existing?.syncStatus == SyncStatus.rejected
        ? _existing?.syncError
        : null;

    // Not lazy: the form is short and every field keeps its state while scrolled away.
    return SingleChildScrollView(
      padding: const EdgeInsets.all(AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (syncError != null) ...[
            NCard(
              title: 'Sunucu bu kaydı kabul etmedi',
              description: '$syncError\nDüzeltip kaydet ya da sil.',
            ),
            const SizedBox(height: AppSpacing.lg),
          ],
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: const Icon(Icons.event_outlined),
            title: const Text('Tarih'),
            subtitle: Text(formatDay(_date)),
            onTap: _pickDate,
          ),
          DropdownButtonFormField<String>(
            initialValue: source,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'Kaynak'),
            items: [
              for (final s in sources)
                DropdownMenuItem(value: s.id, child: Text(s.name)),
              const DropdownMenuItem(
                value: _otherSource,
                child: Text('Diğer (adını yaz)'),
              ),
            ],
            onChanged: (v) => setState(() => _source = v),
          ),
          if (source == _otherSource) ...[
            const SizedBox(height: AppSpacing.md),
            TextField(
              controller: _sourceName,
              maxLength: TopicTestRules.sourceNameMaxLength,
              decoration: const InputDecoration(labelText: 'Kaynağın adı'),
            ),
          ],
          const SizedBox(height: AppSpacing.xl),
          Text('Konular', style: textTheme.titleSmall),
          const SizedBox(height: AppSpacing.sm),
          for (final row in _rows)
            Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.md),
              child: _ResultCard(
                key: ObjectKey(row),
                draft: row,
                name: topicName(row.topicId, topics),
                onChanged: () => setState(() {}),
                onRemove: () => setState(() {
                  _rows.remove(row);
                  row.dispose();
                }),
              ),
            ),
          if (_rows.length < TopicTestRules.maxTopics)
            NButton(
              label: 'Konu ekle',
              icon: Icons.add,
              variant: NButtonVariant.outline,
              onPressed: _addTopic,
            ),
          const SizedBox(height: AppSpacing.xl),
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Tekrar çözüm'),
            subtitle: const Text('Bu soruları daha önce çözdüm.'),
            value: _isRepeat,
            onChanged: (v) => setState(() => _isRepeat = v),
          ),
          const SizedBox(height: AppSpacing.sm),
          Text('Zorluk (isteğe bağlı)', style: textTheme.titleSmall),
          const SizedBox(height: AppSpacing.sm),
          Wrap(
            spacing: AppSpacing.sm,
            children: [
              for (final d in Difficulty.values)
                ChoiceChip(
                  label: Text(d.label),
                  selected: _difficulty == d,
                  onSelected: (on) =>
                      setState(() => _difficulty = on ? d : null),
                ),
            ],
          ),
          const SizedBox(height: AppSpacing.lg),
          TextField(
            controller: _duration,
            keyboardType: TextInputType.number,
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            decoration: const InputDecoration(
              labelText: 'Süre (dakika, isteğe bağlı)',
            ),
          ),
          const SizedBox(height: AppSpacing.lg),
          TextField(
            controller: _note,
            maxLength: TopicTestRules.noteMaxLength,
            maxLines: 3,
            minLines: 1,
            decoration: const InputDecoration(labelText: 'Not (isteğe bağlı)'),
          ),
          if (_errors.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.md),
            for (final e in _errors)
              Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.xs),
                child: Text(
                  e,
                  style: textTheme.bodyMedium?.copyWith(
                    color: Theme.of(context).colorScheme.error,
                  ),
                ),
              ),
          ],
          const SizedBox(height: AppSpacing.lg),
          NButton(
            label: 'Kaydet',
            expand: true,
            onPressed: _saving ? null : _save,
          ),
        ],
      ),
    );
  }
}

/// Text fields of one topic's answers.
class _ResultDraft {
  _ResultDraft(this.topicId);

  _ResultDraft.from(TopicResult r) : topicId = r.topicId {
    total.text = '${r.total}';
    correct.text = '${r.correct}';
    wrong.text = '${r.wrong}';
    knowledge.text = r.mistakeKnowledge?.toString() ?? '';
    attention.text = r.mistakeAttention?.toString() ?? '';
    time.text = r.mistakeTime?.toString() ?? '';
    showReasons =
        r.mistakeKnowledge != null ||
        r.mistakeAttention != null ||
        r.mistakeTime != null;
  }

  final String topicId;
  final total = TextEditingController();
  final correct = TextEditingController();
  final wrong = TextEditingController();
  final knowledge = TextEditingController();
  final attention = TextEditingController();
  final time = TextEditingController();
  var showReasons = false;

  TopicResult toResult() => TopicResult(
    topicId: topicId,
    total: int.tryParse(total.text) ?? 0,
    correct: int.tryParse(correct.text) ?? 0,
    wrong: int.tryParse(wrong.text) ?? 0,
    mistakeKnowledge: int.tryParse(knowledge.text),
    mistakeAttention: int.tryParse(attention.text),
    mistakeTime: int.tryParse(time.text),
  );

  void dispose() {
    for (final c in [total, correct, wrong, knowledge, attention, time]) {
      c.dispose();
    }
  }
}

class _ResultCard extends StatelessWidget {
  const _ResultCard({
    super.key,
    required this.draft,
    required this.name,
    required this.onChanged,
    required this.onRemove,
  });

  final _ResultDraft draft;
  final String name;
  final VoidCallback onChanged;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final result = draft.toResult();
    final textTheme = Theme.of(context).textTheme;
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(child: Text(name, style: textTheme.titleSmall)),
                IconButton(
                  tooltip: 'Konuyu çıkar',
                  icon: const Icon(Icons.close),
                  onPressed: onRemove,
                ),
              ],
            ),
            Row(
              children: [
                _count(draft.total, 'Soru'),
                const SizedBox(width: AppSpacing.sm),
                _count(draft.correct, 'Doğru'),
                const SizedBox(width: AppSpacing.sm),
                _count(draft.wrong, 'Yanlış'),
              ],
            ),
            const SizedBox(height: AppSpacing.sm),
            Text(
              'Boş ${result.blank} · Net ${formatNet(result.net)}',
              style: textTheme.bodySmall,
            ),
            if (draft.showReasons) ...[
              const SizedBox(height: AppSpacing.md),
              Text('Yanlış nedenleri', style: textTheme.labelMedium),
              const SizedBox(height: AppSpacing.sm),
              Row(
                children: [
                  _count(draft.knowledge, 'Bilgi'),
                  const SizedBox(width: AppSpacing.sm),
                  _count(draft.attention, 'Dikkat'),
                  const SizedBox(width: AppSpacing.sm),
                  _count(draft.time, 'Zaman'),
                ],
              ),
            ] else if (result.wrong > 0)
              TextButton(
                onPressed: () {
                  draft.showReasons = true;
                  onChanged();
                },
                child: const Text('Yanlış nedenlerini ekle'),
              ),
          ],
        ),
      ),
    );
  }

  Widget _count(TextEditingController controller, String label) => Expanded(
    child: TextField(
      controller: controller,
      keyboardType: TextInputType.number,
      inputFormatters: [
        FilteringTextInputFormatter.digitsOnly,
        LengthLimitingTextInputFormatter(3),
      ],
      decoration: InputDecoration(labelText: label),
      onChanged: (_) => onChanged(),
    ),
  );
}

/// Topics of the student's subjects, searchable; returns the picked topic id.
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
            child: widget.subjects.isEmpty
                ? const Center(
                    child: Padding(
                      padding: EdgeInsets.all(AppSpacing.xl),
                      child: Text(
                        'Konu listesi henüz yüklenmedi. İnternete bağlanıp tekrar dene.',
                        textAlign: TextAlign.center,
                      ),
                    ),
                  )
                : ListView.builder(
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
