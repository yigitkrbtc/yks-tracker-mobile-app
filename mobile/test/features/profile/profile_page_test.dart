import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/core/design/design.dart';
import 'package:nirengi/features/profile/domain/study_profile.dart';
import 'package:nirengi/features/profile/presentation/profile_page.dart';

import '../../support/app_fakes.dart';
import '../../support/auth_fakes.dart';
import '../../support/design_host.dart';

void main() {
  late FakeProfileRepository profiles;

  Future<void> pumpOnboarding(WidgetTester tester) async {
    profiles = FakeProfileRepository();
    await tester.pumpWidget(
      ProviderScope(
        overrides: fakeAppOverrides(
          store: MemorySessionStore(refreshToken: 'refresh-old'),
          profiles: profiles,
        ),
        child: designHost(
          ProfilePage(onboarding: true, now: DateTime.utc(2026, 10, 28)),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  NButton startButton(WidgetTester tester) =>
      tester.widget<NButton>(find.widgetWithText(NButton, 'Başla'));

  /// [last] picks the second-score-type row when a label appears twice.
  Future<void> choose(
    WidgetTester tester,
    String label, {
    bool last = false,
  }) async {
    final matches = find.widgetWithText(ChoiceChip, label);
    final chip = last ? matches.last : matches.first;
    await tester.ensureVisible(chip);
    await tester.tap(chip);
    await tester.pumpAndSettle();
  }

  testWidgets('grade suggests the regular exam year and saves the profile', (
    tester,
  ) async {
    await pumpOnboarding(tester);
    expect(startButton(tester).onPressed, isNull);

    await choose(tester, '11. sınıf');
    await choose(tester, 'Eşit Ağırlık');
    await choose(tester, 'Sayısal', last: true);
    expect(startButton(tester).onPressed, isNotNull);

    await tester.ensureVisible(find.widgetWithText(NButton, 'Başla'));
    await tester.tap(find.widgetWithText(NButton, 'Başla'));
    await tester.pumpAndSettle();

    final saved = profiles.saved.single;
    expect(saved.grade, Grade.eleventh);
    expect(saved.primaryScoreType, ScoreType.ea);
    expect(saved.secondaryScoreType, ScoreType.say);
    expect(saved.targetExamYear, 2028);
    expect(saved.ydtLanguage, isNull);
  });

  testWidgets('DIL asks for the YDT language before it can be saved', (
    tester,
  ) async {
    await pumpOnboarding(tester);
    await choose(tester, 'Mezun');
    await choose(tester, 'Dil');

    expect(find.text('YDT dilin'), findsOneWidget);
    expect(startButton(tester).onPressed, isNull);

    await choose(tester, 'Almanca');
    expect(startButton(tester).onPressed, isNotNull);
  });
}
