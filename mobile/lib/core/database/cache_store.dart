import 'dart:convert';

import 'app_database.dart';

/// JSON documents kept for offline start, keyed by name.
class CacheStore {
  CacheStore(this._db);

  final AppDatabase _db;

  Future<Map<String, dynamic>?> read(String key) async {
    final row = await (_db.select(
      _db.cacheEntries,
    )..where((c) => c.key.equals(key))).getSingleOrNull();
    return row == null ? null : jsonDecode(row.value) as Map<String, dynamic>;
  }

  Future<void> write(String key, Map<String, dynamic> value) => _db
      .into(_db.cacheEntries)
      .insertOnConflictUpdate(
        CacheEntriesCompanion.insert(key: key, value: jsonEncode(value)),
      );

  Future<void> remove(String key) =>
      (_db.delete(_db.cacheEntries)..where((c) => c.key.equals(key))).go();
}
