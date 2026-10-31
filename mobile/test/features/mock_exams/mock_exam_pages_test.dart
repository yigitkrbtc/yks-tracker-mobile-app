import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/app/app.dart';

import '../../support/app_fakes.dart';
import '../../support/auth_fakes.dart';
import '../../support/sync_fakes.dart';

void main() {
  late FakeTrackingServer server;
  late FakeApi api;

  setUp(() {
    server = FakeTrackingServer();
    api = FakeApi()..route = server.handle;
  });

  Future<void> pumpApp(WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: fakeAppOverrides(
          store: MemorySessionStore(refreshToken: 'refresh-old'),
          api: api,
        ),
        child: const NirengiApp(),
      ),
    );
    await tester.pumpAndSettle();
  }

  /// Drift closes cancelled query streams on a zero timer; let it run before the test ends.
  Future<void> disposeApp(WidgetTester tester) async {
    await tester.pumpWidget(const SizedBox());
    await tester.pump(Duration.zero);
  }

  Finder field(String section, String label) => find.descendant(
    of: find.byKey(ValueKey(section)),
    matching: find.widgetWithText(TextField, label),
  );

  Future<void> enter(WidgetTester tester, Finder finder, String text) async {
    await tester.ensureVisible(finder);
    await tester.enterText(finder, text);
    await tester.pumpAndSettle();
  }

  Future<void> tapText(WidgetTester tester, String text) async {
    await tester.ensureVisible(find.text(text).last);
    await tester.tap(find.text(text).last);
    await tester.pumpAndSettle();
  }

  Future<void> openNewExam(WidgetTester tester) async {
    await tester.tap(find.text('Denemeler'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Deneme ekle'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Yayınevi'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Örnek Yayınları').last);
    await tester.pumpAndSettle();
  }

  testWidgets('records a TYT exam, tags a wrong answer and syncs it', (
    tester,
  ) async {
    await pumpApp(tester);
    await tester.tap(find.text('Denemeler'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Henüz deneme yok'), findsOneWidget);
    await tester.tap(find.text('Deneme ekle'));
    await tester.pumpAndSettle();

    await tapText(tester, 'Kaydet');
    expect(
      find.text('Listeden bir yayınevi seç ya da adını yaz.'),
      findsOneWidget,
    );
    expect(
      find.text('En az bir bölümün doğru ve yanlışını gir.'),
      findsOneWidget,
    );

    await tester.ensureVisible(find.text('Yayınevi'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Yayınevi'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Örnek Yayınları').last);
    await tester.pumpAndSettle();

    await enter(tester, field('TYT-TUR', 'Doğru'), '30');
    await enter(tester, field('TYT-TUR', 'Yanlış'), '8');
    expect(find.text('Boş 2 · Net 28'), findsOneWidget);
    await enter(tester, field('TYT-SOS.TAR', 'Doğru'), '4');
    await enter(tester, field('TYT-SOS.TAR', 'Yanlış'), '2');
    await tapText(tester, 'Kaydet');
    expect(
      find.text('Tarih: doğru + yanlış soru sayısını aşamaz.'),
      findsOneWidget,
    );

    await enter(tester, field('TYT-SOS.TAR', 'Doğru'), '3');
    await enter(tester, field('TYT-SOS.TAR', 'Yanlış'), '1');
    expect(find.text('Toplam net 30,75'), findsOneWidget);
    await tapText(tester, 'Kaydet');
    expect(find.text('Deneme kaydedildi'), findsOneWidget);
    await tapText(tester, 'Konulara ayır');

    expect(find.text('Yanlışları konulara ayır'), findsOneWidget);
    expect(find.text('Yanlış 0/1 · Boş 0/1'), findsOneWidget);
    // The second "Konu ekle" belongs to Sosyal Bilimler · Tarih (after Türkçe).
    await tester.tap(find.text('Konu ekle').last);
    await tester.pumpAndSettle();
    expect(find.text('Türkçe konu 1'), findsNothing);
    await tester.tap(find.text('Tarih konu 1').first);
    await tester.pumpAndSettle();
    expect(find.text('Yanlış 1/1 · Boş 0/1'), findsOneWidget);
    await tapText(tester, 'Kaydet');

    expect(find.textContaining('Net 30,75'), findsOneWidget);
    expect(find.text('Yanlışlar etiketlenmedi'), findsOneWidget);

    await tester.pump(const Duration(seconds: 1));
    await tester.pumpAndSettle();
    final stored = server.exams.values.single;
    expect(stored['kind'], 'Tyt');
    expect(stored['sourceId'], 'src-pub');
    expect(stored['sections'], hasLength(2));
    final mark =
        (stored['marks'] as List<dynamic>).single as Map<String, dynamic>;
    expect(mark['sectionCode'], 'TYT-SOS.TAR');
    expect(mark['wrong'], 1);
    expect(find.text('Gönderilmeyi bekliyor'), findsNothing);
    await disposeApp(tester);
  });

  testWidgets('a block can be entered as its total; religion can be swapped', (
    tester,
  ) async {
    await pumpApp(tester);
    await openNewExam(tester);

    await tapText(tester, 'Din Kültürü ve Ahlak Bilgisi yerine felsefe çözdüm');
    expect(find.byKey(const ValueKey('TYT-SOS.FEL2')), findsOneWidget);
    expect(find.byKey(const ValueKey('TYT-SOS.DKAB')), findsNothing);

    await tester.ensureVisible(find.text('Yalnız toplamı gir').first);
    await tester.tap(find.text('Yalnız toplamı gir').first);
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('TYT-SOS.TAR')), findsNothing);
    await enter(tester, field('TYT-SOS', 'Doğru'), '12');
    await enter(tester, field('TYT-SOS', 'Yanlış'), '4');
    await tapText(tester, 'Kaydet');
    await tapText(tester, 'Sonra');

    expect(find.textContaining('Sosyal Bilimler 11'), findsOneWidget);
    await disposeApp(tester);
  });

  testWidgets('a branch exam keeps its own question count', (tester) async {
    await pumpApp(tester);
    await openNewExam(tester);
    await tester.tap(find.text('Branş'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Bölüm'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('TYT Temel Matematik').last);
    await tester.pumpAndSettle();
    final branch = find.byKey(const ValueKey('branch-TYT-MAT'));
    Finder branchField(String label) => find.descendant(
      of: branch,
      matching: find.widgetWithText(TextField, label),
    );
    expect(
      tester.widget<TextField>(branchField('Soru')).controller!.text,
      '40',
    );
    await enter(tester, branchField('Soru'), '30');
    await enter(tester, branchField('Doğru'), '20');
    await enter(tester, branchField('Yanlış'), '4');
    expect(find.text('Boş 6 · Net 19'), findsOneWidget);
    await tapText(tester, 'Kaydet');
    await tapText(tester, 'Sonra');

    expect(find.text('Branş'), findsOneWidget);
    await tester.pump(const Duration(seconds: 1));
    await tester.pumpAndSettle();
    final section =
        (server.exams.values.single['sections'] as List<dynamic>).single
            as Map<String, dynamic>;
    expect(section['questionCount'], 30);
    await disposeApp(tester);
  });
}
