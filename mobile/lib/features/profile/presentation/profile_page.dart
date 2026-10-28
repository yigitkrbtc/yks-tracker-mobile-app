import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design/design.dart';
import '../application/profile_providers.dart';
import '../domain/exam_calendar.dart';
import '../domain/profile_repository.dart';
import '../domain/study_profile.dart';

/// Onboarding (no profile yet) and later edits share one form.
class ProfilePage extends ConsumerStatefulWidget {
  const ProfilePage({super.key, this.onboarding = false, this.now});

  static const onboardingPath = '/onboarding';
  static const editPath = '/profile';

  final bool onboarding;

  /// Fixed clock for tests.
  final DateTime? now;

  @override
  ConsumerState<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends ConsumerState<ProfilePage> {
  final _goal = TextEditingController();
  late final List<int> _years = ExamCalendar.selectableYears(
    widget.now ?? DateTime.now(),
  );
  Grade? _grade;
  ScoreType? _primary;
  ScoreType? _secondary;
  YdtLanguage? _language;
  int? _year;
  var _yearTouched = false;
  var _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    final existing = ref.read(studyProfileProvider).value;
    if (existing != null) {
      _grade = existing.grade;
      _primary = existing.primaryScoreType;
      _secondary = existing.secondaryScoreType;
      _language = existing.ydtLanguage;
      _year = _years.contains(existing.targetExamYear)
          ? existing.targetExamYear
          : null;
      _yearTouched = true;
      _goal.text = existing.weeklyQuestionGoal?.toString() ?? '';
    }
  }

  @override
  void dispose() {
    _goal.dispose();
    super.dispose();
  }

  bool get _needsLanguage =>
      _primary == ScoreType.dil || _secondary == ScoreType.dil;

  int? get _goalValue => int.tryParse(_goal.text.trim());

  bool get _goalValid {
    if (_goal.text.trim().isEmpty) {
      return true;
    }
    final goal = _goalValue;
    return goal != null &&
        goal >= 1 &&
        goal <= StudyProfile.maxWeeklyQuestionGoal;
  }

  bool get _complete =>
      _grade != null &&
      _primary != null &&
      _year != null &&
      (!_needsLanguage || _language != null) &&
      _goalValid;

  void _selectGrade(Grade grade) => setState(() {
    _grade = grade;
    // Suggest the student's regular exam year until they pick one themselves.
    if (!_yearTouched) {
      _year = _years[grade.sittingsAhead.clamp(0, _years.length - 1)];
    }
  });

  void _selectPrimary(ScoreType type) => setState(() {
    _primary = type;
    if (_secondary == type) {
      _secondary = null;
    }
    if (!_needsLanguage) {
      _language = null;
    }
  });

  void _selectSecondary(ScoreType? type) => setState(() {
    _secondary = type;
    if (!_needsLanguage) {
      _language = null;
    }
  });

  Future<void> _save() async {
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ref
          .read(studyProfileProvider.notifier)
          .save(
            StudyProfile(
              grade: _grade!,
              primaryScoreType: _primary!,
              secondaryScoreType: _secondary,
              ydtLanguage: _needsLanguage ? _language : null,
              targetExamYear: _year!,
              weeklyQuestionGoal: _goalValue,
            ),
          );
      // Onboarding: the router moves on once the profile exists.
      if (mounted && !widget.onboarding) {
        context.pop();
      }
    } on ProfileException catch (e) {
      setState(() => _error = e.message);
    } finally {
      if (mounted) {
        setState(() => _saving = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.onboarding ? 'Hoş geldin' : 'Çalışma profilim'),
        automaticallyImplyLeading: !widget.onboarding,
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(AppSpacing.xl),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (widget.onboarding) ...[
                    Text(
                      'Sana uygun konuları gösterebilmemiz için birkaç bilgi lazım. '
                      'Bunları sonra da değiştirebilirsin.',
                      style: textTheme.bodyMedium,
                    ),
                    const SizedBox(height: AppSpacing.xl),
                  ],
                  _Section(
                    title: 'Sınıfın',
                    child: _Choices<Grade>(
                      options: Grade.values,
                      selected: _grade,
                      label: (g) => g.label,
                      onSelected: _selectGrade,
                    ),
                  ),
                  _Section(
                    title: 'Puan türün',
                    child: _Choices<ScoreType>(
                      options: ScoreType.values,
                      selected: _primary,
                      label: (t) => t.label,
                      onSelected: _selectPrimary,
                    ),
                  ),
                  if (_primary != null)
                    _Section(
                      title: 'İkinci puan türü (isteğe bağlı)',
                      hint: 'Örneğin SAY ile EA arasında kararsızsan.',
                      child: _Choices<ScoreType?>(
                        options: [
                          null,
                          ...ScoreType.values.where((t) => t != _primary),
                        ],
                        selected: _secondary,
                        label: (t) => t?.label ?? 'Yok',
                        onSelected: _selectSecondary,
                      ),
                    ),
                  if (_needsLanguage)
                    _Section(
                      title: 'YDT dilin',
                      child: _Choices<YdtLanguage>(
                        options: YdtLanguage.values,
                        selected: _language,
                        label: (l) => l.label,
                        onSelected: (l) => setState(() => _language = l),
                      ),
                    ),
                  _Section(
                    title: 'Hedef YKS yılın',
                    child: _Choices<int>(
                      options: _years,
                      selected: _year,
                      label: (y) => '$y',
                      onSelected: (y) => setState(() {
                        _year = y;
                        _yearTouched = true;
                      }),
                    ),
                  ),
                  _Section(
                    title: 'Haftalık soru hedefin (isteğe bağlı)',
                    child: TextField(
                      controller: _goal,
                      keyboardType: TextInputType.number,
                      inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                      onChanged: (_) => setState(() {}),
                      decoration: InputDecoration(
                        hintText: 'Örn. 500',
                        errorText: _goalValid
                            ? null
                            : '1 ile ${StudyProfile.maxWeeklyQuestionGoal} arasında olmalı.',
                      ),
                    ),
                  ),
                  if (_error != null) ...[
                    Text(
                      _error!,
                      style: textTheme.bodyMedium?.copyWith(
                        color: Theme.of(context).colorScheme.error,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),
                  ],
                  NButton(
                    label: widget.onboarding ? 'Başla' : 'Kaydet',
                    expand: true,
                    onPressed: _complete && !_saving ? _save : null,
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _Section extends StatelessWidget {
  const _Section({required this.title, required this.child, this.hint});

  final String title;
  final String? hint;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: textTheme.titleSmall),
          if (hint != null)
            Padding(
              padding: const EdgeInsets.only(top: AppSpacing.xs),
              child: Text(hint!, style: textTheme.bodySmall),
            ),
          const SizedBox(height: AppSpacing.sm),
          child,
        ],
      ),
    );
  }
}

class _Choices<T> extends StatelessWidget {
  const _Choices({
    required this.options,
    required this.selected,
    required this.label,
    required this.onSelected,
  });

  final List<T> options;
  final T? selected;
  final String Function(T) label;
  final ValueChanged<T> onSelected;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: AppSpacing.sm,
      runSpacing: AppSpacing.sm,
      children: [
        for (final option in options)
          ChoiceChip(
            label: Text(label(option)),
            selected: option == selected,
            onSelected: (_) => onSelected(option),
          ),
      ],
    );
  }
}
