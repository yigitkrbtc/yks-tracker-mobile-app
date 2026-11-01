import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/app/app.dart';
import 'package:nirengi/features/analytics/domain/analysis_repository.dart';

import '../../support/analysis_fakes.dart';
import '../../support/app_fakes.dart';
import '../../support/auth_fakes.dart';

void main() {
  Future<void> pumpApp(
    WidgetTester tester,
    FakeAnalysisRepository analysis,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: fakeAppOverrides(
          store: MemorySessionStore(refreshToken: 'refresh-old'),
          analysis: analysis,
        ),
        child: const NirengiApp(),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> disposeApp(WidgetTester tester) async {
    await tester.pumpWidget(const SizedBox());
    await tester.pump(Duration.zero);
  }

  Future<void> openPanel(WidgetTester tester) async {
    await tester.tap(find.text('Analiz'));
    await tester.pumpAndSettle();
  }

  testWidgets('panel shows priorities, test mastery and mock form', (
    tester,
  ) async {
    final analysis = FakeAnalysisRepository(
      overview: overviewJson(
        priorities: [
          {
            'topicId': 'TYT.TUR-1',
            'priority': 1.2,
            'mastery': 0.42,
            'expectedQuestions': 4.6,
            'gain': 1.75,
            'mockBoost': 0,
          },
        ],
        tests: [
          aggregateJson('TYT-TUR'),
          aggregateJson('AYT-SB2', isActive: false),
        ],
        subjects: [aggregateJson('TYT.TUR')],
        sections: [
          sectionJson('TYT-TUR', gapWarning: true),
          sectionJson('TYT-SOS.TAR', exams: 1),
        ],
        topics: [topicStatusJson('TYT.TUR-1')],
      ),
      cards: {'TYT.TUR-1': topicCardJson('TYT.TUR-1')},
    );
    await pumpApp(tester, analysis);
    await openPanel(tester);

    expect(find.text('Sıradaki odak'), findsOneWidget);
    expect(find.text('Türkçe konu 1'), findsWidgets);
    expect(
      find.textContaining('Ustalık %42 · sınavda ≈4,6 soru · kazanç ≈1,8 net'),
      findsOneWidget,
    );
    expect(find.text('TYT Türkçe'), findsOneWidget);
    expect(find.textContaining('AYT'), findsNothing);
    await tester.scrollUntilVisible(find.text('Deneme formu'), 200);
    expect(find.text('Yükseliyor'), findsOneWidget);
    expect(
      find.textContaining('Konu testlerinde %90, denemede %70'),
      findsOneWidget,
    );
    await tester.scrollUntilVisible(find.text('Sosyal Bilimler · Tarih'), 200);
    expect(
      find.textContaining('1 deneme · güvenilir bir form için 2 deneme daha'),
      findsOneWidget,
    );

    await tester.scrollUntilVisible(find.text('Sıradaki odak'), -200);
    await tester.tap(find.textContaining('Ustalık %42'));
    await tester.pumpAndSettle();

    expect(analysis.topicRequests, ['TYT.TUR-1']);
    expect(find.text('Ustalık %55'), findsOneWidget);
    expect(find.text('Gelişiyor'), findsOneWidget);
    expect(find.text('Güven: Ön bulgu'), findsOneWidget);
    expect(
      find.text('"Makul" için yaklaşık 11 yeni soru daha.'),
      findsOneWidget,
    );
    expect(find.text('Düşüyor'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('Bu konuda test ekle'), 200);
    expect(
      find.text('3 kayıt · 60 soru · 33 doğru, 18 yanlış, 9 boş'),
      findsOneWidget,
    );

    await tester.tap(find.text('Bu konuda test ekle'));
    await tester.pumpAndSettle();
    expect(find.text('Türkçe konu 1'), findsWidgets);
    expect(find.text('Kaydet'), findsOneWidget);
    await disposeApp(tester);
  });

  testWidgets('a topic without records explains what is missing', (
    tester,
  ) async {
    await pumpApp(tester, FakeAnalysisRepository());
    await openPanel(tester);

    expect(find.text('Analiz için henüz kayıt yok'), findsOneWidget);
    expect(find.text('Veri az, ağırlığı yüksek'), findsOneWidget);
    await tester.tap(find.text('Türkçe konu 1'));
    await tester.pumpAndSettle();

    expect(find.text('Bu konuda henüz kayıt yok'), findsOneWidget);
    expect(find.textContaining('"Ön bulgu" için yaklaşık 11'), findsOneWidget);
    await disposeApp(tester);
  });

  testWidgets('topic list shows the status dot and opens the analysis', (
    tester,
  ) async {
    final analysis = FakeAnalysisRepository(
      overview: overviewJson(
        topics: [topicStatusJson('TYT.TUR-1', mastery: 0.85, band: 'Strong')],
      ),
      cards: {'TYT.TUR-1': topicCardJson('TYT.TUR-1')},
    );
    await pumpApp(tester, analysis);
    await tester.tap(find.text('Türkçe'));
    await tester.pumpAndSettle();

    expect(find.byKey(const ValueKey('status-TYT.TUR-1')), findsOneWidget);
    expect(find.byTooltip('Güçlü · %85'), findsOneWidget);

    await tester.tap(find.text('Türkçe konu 1'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Analizi gör'));
    await tester.pumpAndSettle();

    expect(find.text('Ustalık %55'), findsOneWidget);
    await disposeApp(tester);
  });

  testWidgets('offline without a downloaded panel offers a retry', (
    tester,
  ) async {
    final analysis = FakeAnalysisRepository()
      ..error = const AnalysisException(
        'Sunucuya ulaşılamadı.',
        isOffline: true,
      );
    await pumpApp(tester, analysis);
    await openPanel(tester);

    expect(find.text('Sunucuya ulaşılamadı.'), findsOneWidget);
    analysis.error = null;
    await tester.tap(find.text('Tekrar dene'));
    await tester.pumpAndSettle();
    expect(find.text('Analiz için henüz kayıt yok'), findsOneWidget);
    await disposeApp(tester);
  });
}
