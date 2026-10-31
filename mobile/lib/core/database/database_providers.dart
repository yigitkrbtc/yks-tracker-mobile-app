import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app_database.dart';

/// Bound to the on-device database in `app/composition.dart`; tests bind an in-memory one.
final appDatabaseProvider = Provider<AppDatabase>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);
