import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Layering rules from CLAUDE.md: core knows no feature; a feature's domain is plain Dart.
void main() {
  Iterable<File> dartFiles(String dir) => Directory(dir)
      .listSync(recursive: true)
      .whereType<File>()
      .where((f) => f.path.endsWith('.dart') && !f.path.endsWith('.g.dart'));

  List<String> offenders(Iterable<File> files, RegExp forbidden) => [
    for (final f in files)
      if (forbidden.hasMatch(f.readAsStringSync())) f.path,
  ];

  test('core does not import features or the app shell', () {
    expect(
      offenders(
        dartFiles('lib/core'),
        RegExp(r"import '(\.\./)+(features|app)/"),
      ),
      isEmpty,
    );
  });

  test(
    'feature domain layers import no framework, storage or network code',
    () {
      final domainFiles = dartFiles('lib/features')
          .where((f) => f.path.contains('/domain/'));
      expect(domainFiles, isNotEmpty);
      expect(
        offenders(
          domainFiles,
          RegExp(
            r"import 'package:(flutter|flutter_riverpod|drift|dio)/|import '(\.\./)+(core|app)/",
          ),
        ),
        isEmpty,
      );
    },
  );
}
