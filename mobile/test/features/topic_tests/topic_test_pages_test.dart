import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/app/app.dart';

import '../../support/shell.dart';
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

  Future<void> fillCounts(
    WidgetTester tester, {
    required String total,
    required String correct,
    required String wrong,
  }) async {
    await tester.enterText(find.widgetWithText(TextField, 'Soru'), total);
    await tester.enterText(find.widgetWithText(TextField, 'Doğru'), correct);
    await tester.enterText(find.widgetWithText(TextField, 'Yanlış'), wrong);
    await tester.pumpAndSettle();
  }

  Future<void> save(WidgetTester tester) async {
    await tester.ensureVisible(find.text('Kaydet'));
    await tester.tap(find.text('Kaydet'));
    await tester.pumpAndSettle();
  }

  testWidgets('records a test, lists it and sends it to the server', (
    tester,
  ) async {
    await pumpApp(tester);
    await tester.tap(find.text('Testler'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Henüz konu testi yok'), findsOneWidget);

    await tester.tap(find.text('Test ekle'));
    await tester.pumpAndSettle();
    expect(find.text('Konu testi ekle'), findsOneWidget);

    await save(tester);
    expect(
      find.text('Listeden bir kaynak seç ya da kaynağın adını yaz.'),
      findsOneWidget,
    );
    expect(
      find.text('Bir testte 1 ile 20 arasında konu olur.'),
      findsOneWidget,
    );

    await tester.tap(find.text('Kaynak'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('ÖSYM çıkmış sorular').last);
    await tester.pumpAndSettle();

    await tester.tap(find.text('Konu ekle'));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.widgetWithText(TextField, 'Konu ara'),
      'türkçe',
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Türkçe konu 1'));
    await tester.pumpAndSettle();

    await fillCounts(tester, total: '20', correct: '15', wrong: '4');
    expect(find.text('Boş 1 · Net 14'), findsOneWidget);
    await tester.tap(find.text('Yanlış nedenlerini ekle'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'Dikkat'), '5');
    await save(tester);
    expect(
      find.text('Yanlış nedenlerinin toplamı yanlış sayısını aşamaz.'),
      findsOneWidget,
    );
    await tester.enterText(find.widgetWithText(TextField, 'Dikkat'), '2');
    await save(tester);

    expect(find.text('Türkçe konu 1'), findsOneWidget);
    expect(find.text('D 15 · Y 4 · B 1 · Net 14'), findsOneWidget);
    expect(find.textContaining('ÖSYM çıkmış sorular'), findsOneWidget);

    // The scheduler pushes shortly after the write.
    await tester.pump(const Duration(seconds: 1));
    await tester.pumpAndSettle();
    final stored = server.records.values.single;
    expect(stored['sourceId'], 'src-osym');
    expect(firstResult(stored)['mistakeAttention'], 2);
    expect(find.text('Gönderilmeyi bekliyor'), findsNothing);
    await disposeApp(tester);
  });

  testWidgets('offline write shows as waiting; edit and delete work', (
    tester,
  ) async {
    api.route = FakeApi.offline;
    await pumpApp(tester);

    // From the topic sheet, the form starts with that topic.
    await openTopics(tester);
    await tester.tap(find.text('Türkçe'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Türkçe konu 1'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Bu konuda test ekle'));
    await tester.pumpAndSettle();
    expect(find.text('Türkçe konu 1'), findsOneWidget);

    await tester.tap(find.text('Kaynak'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Diğer (adını yaz)').last);
    await tester.pumpAndSettle();
    await tester.enterText(
      find.widgetWithText(TextField, 'Kaynağın adı'),
      'Okul fasikülü',
    );
    await fillCounts(tester, total: '10', correct: '8', wrong: '2');
    await save(tester);

    await tester.tap(find.text('Testler'));
    await tester.pump(const Duration(seconds: 1));
    await tester.pumpAndSettle();
    expect(find.text('Gönderilmeyi bekliyor'), findsOneWidget);
    expect(find.text('D 8 · Y 2 · B 0 · Net 7,5'), findsOneWidget);

    await tester.tap(find.textContaining('Okul fasikülü'));
    await tester.pumpAndSettle();
    expect(find.text('Testi düzenle'), findsOneWidget);
    await tester.enterText(find.widgetWithText(TextField, 'Doğru'), '7');
    await save(tester);
    expect(find.text('D 7 · Y 2 · B 1 · Net 6,5'), findsOneWidget);

    await tester.tap(find.textContaining('Okul fasikülü'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Sil'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, 'Sil'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Henüz konu testi yok'), findsOneWidget);
    await disposeApp(tester);
  });

  testWidgets('a retired last source is not preselected for a new test', (
    tester,
  ) async {
    server.records['old'] = {
      'id': 'old',
      'studiedOn': '2026-10-29',
      'sourceId': 'src-retired',
      'sourceName': null,
      'isRepeat': false,
      'difficulty': null,
      'durationMinutes': null,
      'note': null,
      'results': [
        {
          'topicId': 'TYT.TUR.01',
          'total': 10,
          'correct': 7,
          'wrong': 2,
          'blank': 1,
          'net': 6.5,
        },
      ],
      'clientModifiedAt': '2026-10-29T18:30:00.000000+00:00',
      'deleted': false,
      'changeSeq': ++server.seq,
    };
    await pumpApp(tester);
    await tester.tap(find.text('Testler'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Test ekle'));
    await tester.pumpAndSettle();

    await save(tester);

    expect(
      find.text('Listeden bir kaynak seç ya da kaynağın adını yaz.'),
      findsOneWidget,
    );
    await disposeApp(tester);
  });
}
