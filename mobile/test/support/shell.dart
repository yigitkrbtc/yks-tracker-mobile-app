import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

/// The app opens on Today (S6); topic tests start from the Konular tab.
Future<void> openTopics(WidgetTester tester) async {
  await tester.tap(find.text('Konular'));
  await tester.pumpAndSettle();
}

/// Drift closes cancelled query streams on a zero timer (Today watches the records); let it run before the
/// test ends.
Future<void> disposeShell(WidgetTester tester) async {
  await tester.pumpWidget(const SizedBox());
  await tester.pump(Duration.zero);
}
