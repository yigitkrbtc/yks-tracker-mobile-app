import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// ADR-0011: only the design system and the app shell may depend on shadcn_ui.
void main() {
  test('shadcn_ui is imported only from core/design and app', () {
    const allowed = ['lib/core/design/', 'lib/app/'];
    final offenders = Directory('lib')
        .listSync(recursive: true)
        .whereType<File>()
        .where((f) => f.path.endsWith('.dart'))
        .where((f) => !allowed.any((a) => f.path.startsWith(a)))
        .where((f) => f.readAsStringSync().contains('package:shadcn_ui/'))
        .map((f) => f.path)
        .toList();

    expect(offenders, isEmpty, reason: 'Use core/design/design.dart instead.');
  });
}
