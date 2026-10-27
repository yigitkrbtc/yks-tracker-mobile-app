import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../domain/auth_models.dart';
import '../domain/auth_repository.dart';

/// Keychain / Keystore backed; the refresh token never touches plain preferences.
class SecureSessionStore implements SessionStore {
  SecureSessionStore([FlutterSecureStorage? storage])
    : _storage = storage ?? const FlutterSecureStorage();

  static const _refreshKey = 'auth.refresh_token';
  static const _profileKey = 'auth.profile';

  final FlutterSecureStorage _storage;

  @override
  Future<String?> readRefreshToken() => _storage.read(key: _refreshKey);

  @override
  Future<UserProfile?> readProfile() async {
    final raw = await _storage.read(key: _profileKey);
    if (raw == null) {
      return null;
    }
    return UserProfile.fromJson(jsonDecode(raw) as Map<String, dynamic>);
  }

  @override
  Future<void> save({
    required String refreshToken,
    UserProfile? profile,
  }) async {
    await _storage.write(key: _refreshKey, value: refreshToken);
    if (profile != null) {
      await _storage.write(
        key: _profileKey,
        value: jsonEncode(profile.toJson()),
      );
    }
  }

  @override
  Future<void> clear() async {
    await _storage.delete(key: _refreshKey);
    await _storage.delete(key: _profileKey);
  }
}
