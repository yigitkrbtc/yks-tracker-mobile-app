import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/data_export.dart';

/// Bound to the HTTP implementation in `app/composition.dart`; tests bind fakes.
final dataExportServiceProvider = Provider<DataExportService>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);
