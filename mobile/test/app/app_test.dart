import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/app/app.dart';

void main() {
  testWidgets('app starts on the home shell with three tabs', (tester) async {
    await tester.pumpWidget(const ProviderScope(child: NirengiApp()));
    await tester.pumpAndSettle();

    expect(find.text('Nirengi'), findsOneWidget);
    expect(find.byType(NavigationDestination), findsNWidgets(3));
    expect(find.text('Konular yakında'), findsOneWidget);

    await tester.tap(find.text('Denemeler'));
    await tester.pumpAndSettle();
    expect(find.text('Denemeler yakında'), findsOneWidget);
  });
}
