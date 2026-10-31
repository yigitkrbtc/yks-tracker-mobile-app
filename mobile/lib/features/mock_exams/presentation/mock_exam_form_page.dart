import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design/design.dart';
import '../../curriculum/application/curriculum_providers.dart';
import '../../curriculum/domain/curriculum.dart';
import '../../profile/application/profile_providers.dart';
import '../application/mock_exam_providers.dart';
import '../domain/mock_exam.dart';
import 'mock_exam_format.dart';
import 'mock_exam_marks_page.dart';

/// Records a new mock exam or edits one, section by section. Saving never waits for the network.
class MockExamFormPage extends ConsumerStatefulWidget {
  const MockExamFormPage({super.key, this.id, this.now});

  static const newPath = '/mock-exams/new';
  static const editPattern = '/mock-exams/:id';

  static String pathFor(String id) => '/mock-exams/$id';

  /// The exam to edit; null for a new one.
  final String? id;

  /// Fixed clock for tests.
  final DateTime? now;

  @override
  ConsumerState<MockExamFormPage> createState() => _MockExamFormPageState();
}

const _otherSource = '';

class _MockExamFormPageState extends ConsumerState<MockExamFormPage> {
  late final DateTime _now = widget.now ?? DateTime.now();
  late DateTime _date = MockExamRules.todayInTurkey(_now);
  final _sourceName = TextEditingController();
  final _title = TextEditingController();
  final _duration = TextEditingController();
  final _note = TextEditingController();
  final _drafts = <String, _SectionDraft>{};

  /// Block tests (by root code) entered as one total instead of per subject.
  final _totalOnly = <String>{};

  /// Sections replaced by their alternative (religion → philosophy), by the replaced code.
  final _alternative = <String>{};
  var _kind = MockExamKind.tyt;
  String? _branchSection;
  String? _source; // a listed source id, or [_otherSource]
  var _showAllTests = false;
  MockExam? _existing;
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
    final repository = ref.read(mockExamRepositoryProvider);
    final id = widget.id;
    if (id != null) {
      final exam = await repository.get(id);
      if (!mounted) {
        return;
      }
      if (exam == null) {
        setState(() {
          _missing = true;
          _loading = false;
        });
        return;
      }
      _fill(exam);
    } else {
      final last = await repository.lastSource();
      if (!mounted) {
        return;
      }
      if (last != null) {
        _source = last.id ?? _otherSource;
        _sourceName.text = last.id == null ? last.name ?? '' : '';
      }
    }
    setState(() => _loading = false);
  }

  void _fill(MockExam e) {
    _existing = e;
    _kind = e.kind;
    _date = e.takenOn;
    _source = e.sourceId ?? _otherSource;
    _sourceName.text = e.sourceId == null ? e.sourceName ?? '' : '';
    _title.text = e.title ?? '';
    _duration.text = e.durationMinutes?.toString() ?? '';
    _note.text = e.note ?? '';
    final template = {
      for (final s
          in ref.read(curriculumProvider).value?.mockSections ??
              const <MockSection>[])
        s.code: s,
    };
    for (final s in e.sections) {
      _draft(s.sectionCode).fill(s);
      final def = template[s.sectionCode];
      final hasChildren = template.values.any(
        (x) => x.parentCode == s.sectionCode,
      );
      if (def != null && def.isRoot && hasChildren) {
        _totalOnly.add(def.code);
      }
      if (def?.alternativeOf != null) {
        _alternative.add(def!.alternativeOf!);
      }
    }
    if (e.kind == MockExamKind.branch && e.sections.isNotEmpty) {
      _branchSection = e.sections.single.sectionCode;
    }
  }

  _SectionDraft _draft(String code) => _drafts[code] ??= _SectionDraft();

  @override
  void dispose() {
    for (final c in [_sourceName, _title, _duration, _note]) {
      c.dispose();
    }
    for (final d in _drafts.values) {
      d.dispose();
    }
    super.dispose();
  }

  /// Tests of the student's score types; the rest show after "Diğer testler".
  Set<String> _studentTests(Curriculum c) {
    final types = ref.read(studyProfileProvider).value?.scoreTypes;
    if (types == null) {
      return {for (final s in c.mockSections) s.testCode};
    }
    final codes = {for (final t in types) t.code};
    return {
      for (final s in c.scoreTypes)
        if (codes.contains(s.code)) ...s.testCodes,
    };
  }

  /// The section lines currently on screen, in score sheet order.
  List<MockSection> _visibleRows(Curriculum c) {
    if (_kind == MockExamKind.branch) {
      final code = _branchSection;
      return [
        for (final s in c.mockSections)
          if (s.code == code) s,
      ];
    }
    final offered = MockExamRules.sectionsFor(_kind, c);
    final own = _studentTests(c);
    final rows = <MockSection>[];
    for (final root in offered.where((s) => s.isRoot)) {
      final hasData = _drafts.entries.any(
        (d) =>
            d.value.filled &&
            offered.any((s) => s.code == d.key && s.testCode == root.testCode),
      );
      if (!_showAllTests && !own.contains(root.testCode) && !hasData) {
        continue;
      }
      final children = [
        for (final s in offered)
          if (s.parentCode == root.code && s.alternativeOf == null) s,
      ];
      if (children.isEmpty || _totalOnly.contains(root.code)) {
        rows.add(root);
        continue;
      }
      for (final child in children) {
        final alternative = offered
            .where((s) => s.alternativeOf == child.code)
            .firstOrNull;
        rows.add(
          alternative != null && _alternative.contains(child.code)
              ? alternative
              : child,
        );
      }
    }
    return rows;
  }

  MockExam _build(Curriculum c) {
    final other = _source == _otherSource;
    final listed = {for (final s in c.sources) s.id};
    // A preselected source that was retired is not sent; one the stored exam already has stays.
    final hidden = !listed.contains(_source) && _source != _existing?.sourceId;
    final sections = [
      for (final row in _visibleRows(c))
        if (_drafts[row.code]?.filled ?? false)
          _drafts[row.code]!.toResult(
            row,
            ownCount: _kind == MockExamKind.branch,
          ),
    ];
    final kept = {for (final s in sections) s.sectionCode};
    final title = _title.text.trim();
    final note = _note.text.trim();
    return MockExam(
      id: _existing?.id ?? ref.read(newMockExamIdProvider)(),
      kind: _kind,
      takenOn: _date,
      sourceId: other || hidden ? null : _source,
      sourceName: other ? _sourceName.text.trim() : null,
      title: title.isEmpty ? null : title,
      durationMinutes: int.tryParse(_duration.text),
      note: note.isEmpty ? null : note,
      sections: sections,
      // Marks of sections that are gone are dropped; the rest are checked against the new counts.
      marks: [
        for (final m in _existing?.marks ?? const <TopicMark>[])
          if (kept.contains(m.sectionCode)) m,
      ],
      modifiedAt: _now,
    );
  }

  /// Saves; true when stored.
  Future<MockExam?> _store(Curriculum c) async {
    final exam = _build(c);
    final errors = MockExamRules.validate(
      exam,
      c,
      widget.now ?? DateTime.now(),
    );
    setState(() => _errors = errors);
    if (errors.isNotEmpty) {
      return null;
    }
    setState(() => _saving = true);
    await ref.read(mockExamRepositoryProvider).save(exam);
    return exam;
  }

  Future<void> _save(Curriculum c) async {
    final exam = await _store(c);
    if (exam == null || !mounted) {
      return;
    }
    final lost = exam.sections.any((s) => s.lost > 0);
    if (_editing || !lost) {
      context.pop();
      return;
    }
    final tag = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Deneme kaydedildi'),
        content: const Text(
          'Yanlışlarını ve boşlarını konulara ayırmak ister misin? '
          'Hangi konularda soru kaçırdığını görmeni sağlar; sonra da yapabilirsin.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Sonra'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Konulara ayır'),
          ),
        ],
      ),
    );
    if (!mounted) {
      return;
    }
    if (tag == true) {
      context.pushReplacement(MockExamMarksPage.pathFor(exam.id));
    } else {
      context.pop();
    }
  }

  Future<void> _saveAndTag(Curriculum c) async {
    final exam = await _store(c);
    if (exam != null && mounted) {
      context.pushReplacement(MockExamMarksPage.pathFor(exam.id));
    }
  }

  Future<void> _delete() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Deneme silinsin mi?'),
        content: const Text('Bu deneme tüm cihazlarından silinir.'),
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
    await ref.read(mockExamRepositoryProvider).delete(widget.id!);
    if (mounted) {
      context.pop();
    }
  }

  Future<void> _pickDate() async {
    final today = MockExamRules.todayInTurkey(_now);
    final first = MockExamRules.earliestDate(_now);
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

  @override
  Widget build(BuildContext context) {
    final curriculum = ref.watch(curriculumProvider).value;
    return Scaffold(
      appBar: AppBar(
        title: Text(_editing ? 'Denemeyi düzenle' : 'Deneme ekle'),
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
          ? const Center(child: Text('Bu deneme bulunamadı.'))
          : curriculum == null || curriculum.mockSections.isEmpty
          ? const Center(
              child: Padding(
                padding: EdgeInsets.all(AppSpacing.xl),
                child: Text(
                  'Sınav yapısı henüz yüklenmedi. İnternete bağlanıp tekrar dene.',
                  textAlign: TextAlign.center,
                ),
              ),
            )
          : _buildForm(context, curriculum),
    );
  }

  Widget _buildForm(BuildContext context, Curriculum c) {
    final textTheme = Theme.of(context).textTheme;
    final sources = c.sources;
    final listed = {for (final s in sources) s.id};
    final source =
        _source == null || _source == _otherSource || listed.contains(_source)
        ? _source
        : null;
    final syncError = _existing?.syncStatus == SyncStatus.rejected
        ? _existing?.syncError
        : null;
    final exam = _existing;

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
          if (!_editing) ...[
            Text('Deneme türü', style: textTheme.titleSmall),
            const SizedBox(height: AppSpacing.sm),
            Wrap(
              spacing: AppSpacing.sm,
              children: [
                for (final k in MockExamKind.values)
                  ChoiceChip(
                    label: Text(k.label),
                    selected: _kind == k,
                    onSelected: (_) => setState(() {
                      _kind = k;
                      _errors = const [];
                    }),
                  ),
              ],
            ),
            const SizedBox(height: AppSpacing.md),
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
            decoration: const InputDecoration(labelText: 'Yayınevi'),
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
              maxLength: MockExamRules.sourceNameMaxLength,
              decoration: const InputDecoration(labelText: 'Yayınevinin adı'),
            ),
          ],
          const SizedBox(height: AppSpacing.md),
          TextField(
            controller: _title,
            maxLength: MockExamRules.titleMaxLength,
            decoration: const InputDecoration(
              labelText: 'Deneme adı (isteğe bağlı)',
              hintText: 'Örn. Türkiye geneli 3',
            ),
          ),
          const SizedBox(height: AppSpacing.lg),
          Text('Bölümler', style: textTheme.titleSmall),
          const SizedBox(height: AppSpacing.xs),
          Text(
            'Doğru ve yanlışı gir; boş ve net kendiliğinden hesaplanır. '
            'Çözmediğin bölümü boş bırak.',
            style: textTheme.bodySmall,
          ),
          const SizedBox(height: AppSpacing.md),
          if (_kind == MockExamKind.branch)
            ..._branchFields(c)
          else
            ..._sessionFields(c),
          if (_kind != MockExamKind.branch) ...[
            const SizedBox(height: AppSpacing.sm),
            Text(
              'Toplam net ${formatNet(_build(c).net)}',
              style: textTheme.titleSmall,
            ),
          ],
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
            maxLength: MockExamRules.noteMaxLength,
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
            onPressed: _saving ? null : () => _save(c),
          ),
          if (exam != null && exam.sections.any((s) => s.lost > 0)) ...[
            const SizedBox(height: AppSpacing.sm),
            NButton(
              label: exam.needsTagging
                  ? 'Kaydet ve yanlışları konulara ayır'
                  : 'Kaydet ve konu etiketlerini düzenle',
              icon: Icons.label_outline,
              variant: NButtonVariant.outline,
              expand: true,
              onPressed: _saving ? null : () => _saveAndTag(c),
            ),
          ],
        ],
      ),
    );
  }

  List<Widget> _sessionFields(Curriculum c) {
    final offered = MockExamRules.sectionsFor(_kind, c);
    final rows = _visibleRows(c);
    final own = _studentTests(c);
    final widgets = <Widget>[];
    for (final root in offered.where((s) => s.isRoot)) {
      final testRows = rows.where((r) => r.testCode == root.testCode).toList();
      if (testRows.isEmpty) {
        continue;
      }
      final hasChildren = offered.any((s) => s.parentCode == root.code);
      final alternatives = offered.where((s) => s.alternativeOf != null);
      widgets.add(
        Card(
          margin: const EdgeInsets.only(bottom: AppSpacing.md),
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(root.name, style: Theme.of(context).textTheme.titleSmall),
                if (hasChildren)
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    dense: true,
                    title: const Text('Yalnız toplamı gir'),
                    value: _totalOnly.contains(root.code),
                    onChanged: (on) => setState(
                      () => on
                          ? _totalOnly.add(root.code)
                          : _totalOnly.remove(root.code),
                    ),
                  ),
                for (final row in testRows)
                  _SectionRow(
                    key: ValueKey(row.code),
                    section: row,
                    label: row.isRoot && hasChildren
                        ? 'Toplam'
                        : row.isRoot
                        ? null
                        : row.name,
                    draft: _draft(row.code),
                    onChanged: () => setState(() {}),
                  ),
                for (final alt in alternatives)
                  if (testRows.any(
                    (r) => r.code == alt.code || r.code == alt.alternativeOf,
                  ))
                    CheckboxListTile(
                      contentPadding: EdgeInsets.zero,
                      dense: true,
                      title: Text(
                        '${offered.firstWhere((s) => s.code == alt.alternativeOf).name} yerine felsefe çözdüm',
                      ),
                      value: _alternative.contains(alt.alternativeOf),
                      onChanged: (on) => setState(
                        () => on == true
                            ? _alternative.add(alt.alternativeOf!)
                            : _alternative.remove(alt.alternativeOf),
                      ),
                    ),
              ],
            ),
          ),
        ),
      );
    }
    final hidden = offered.where(
      (s) => s.isRoot && !rows.any((r) => r.testCode == s.testCode),
    );
    if (hidden.isNotEmpty && own.isNotEmpty) {
      widgets.add(
        TextButton(
          onPressed: () => setState(() => _showAllTests = true),
          child: Text(
            'Diğer testleri göster (${hidden.map((s) => s.name).join(', ')})',
          ),
        ),
      );
    }
    return widgets;
  }

  List<Widget> _branchFields(Curriculum c) {
    final code = _branchSection;
    final section = c.mockSections.where((s) => s.code == code).firstOrNull;
    return [
      DropdownButtonFormField<String>(
        initialValue: code,
        isExpanded: true,
        decoration: const InputDecoration(labelText: 'Bölüm'),
        items: [
          for (final s in c.mockSections)
            DropdownMenuItem(
              value: s.code,
              child: Text(
                '${c.sessionOfTest(s.testCode) == ExamSession.tyt ? 'TYT' : 'AYT'} '
                '${sectionLabel(s.code, c)}',
              ),
            ),
        ],
        onChanged: (v) => setState(() {
          _branchSection = v;
          final def = c.mockSections.firstWhere((s) => s.code == v);
          final draft = _draft(def.code);
          if (draft.questionCount.text.isEmpty) {
            draft.questionCount.text = '${def.questionCount}';
          }
        }),
      ),
      const SizedBox(height: AppSpacing.md),
      if (section != null)
        _SectionRow(
          key: ValueKey('branch-${section.code}'),
          section: section,
          label: null,
          draft: _draft(section.code),
          ownCount: true,
          onChanged: () => setState(() {}),
        ),
    ];
  }
}

/// Text fields of one section's answers.
class _SectionDraft {
  final questionCount = TextEditingController();
  final correct = TextEditingController();
  final wrong = TextEditingController();

  /// Solved: at least one count typed (a section left fully blank is entered as 0 and 0).
  bool get filled => correct.text.isNotEmpty || wrong.text.isNotEmpty;

  void fill(MockSectionResult s) {
    questionCount.text = '${s.questionCount}';
    correct.text = '${s.correct}';
    wrong.text = '${s.wrong}';
  }

  MockSectionResult toResult(MockSection def, {required bool ownCount}) =>
      MockSectionResult(
        sectionCode: def.code,
        questionCount: ownCount
            ? int.tryParse(questionCount.text) ?? def.questionCount
            : def.questionCount,
        correct: int.tryParse(correct.text) ?? 0,
        wrong: int.tryParse(wrong.text) ?? 0,
      );

  void dispose() {
    for (final c in [questionCount, correct, wrong]) {
      c.dispose();
    }
  }
}

class _SectionRow extends StatelessWidget {
  const _SectionRow({
    super.key,
    required this.section,
    required this.label,
    required this.draft,
    required this.onChanged,
    this.ownCount = false,
  });

  final MockSection section;

  /// Shown above the fields; null for a single-section test (the card title says it).
  final String? label;
  final _SectionDraft draft;
  final VoidCallback onChanged;

  /// Branch exams type their own question count.
  final bool ownCount;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final result = draft.toResult(section, ownCount: ownCount);
    return Padding(
      padding: const EdgeInsets.only(top: AppSpacing.sm),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (label != null)
            Text(
              '$label (${section.questionCount} soru)',
              style: textTheme.labelMedium,
            ),
          Row(
            children: [
              if (ownCount) ...[
                _count(draft.questionCount, 'Soru'),
                const SizedBox(width: AppSpacing.sm),
              ],
              _count(draft.correct, 'Doğru'),
              const SizedBox(width: AppSpacing.sm),
              _count(draft.wrong, 'Yanlış'),
            ],
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            draft.filled
                ? 'Boş ${result.blank} · Net ${formatNet(result.net)}'
                : 'Çözülmedi',
            style: textTheme.bodySmall?.copyWith(
              color: draft.filled && result.blank < 0
                  ? Theme.of(context).colorScheme.error
                  : null,
            ),
          ),
        ],
      ),
    );
  }

  Widget _count(TextEditingController controller, String label) => Expanded(
    child: TextField(
      controller: controller,
      keyboardType: TextInputType.number,
      inputFormatters: [
        FilteringTextInputFormatter.digitsOnly,
        LengthLimitingTextInputFormatter(2),
      ],
      decoration: InputDecoration(labelText: label),
      onChanged: (_) => onChanged(),
    ),
  );
}
