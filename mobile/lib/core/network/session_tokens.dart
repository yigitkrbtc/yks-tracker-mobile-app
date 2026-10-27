import 'package:flutter_riverpod/flutter_riverpod.dart';

/// What the HTTP layer needs from the signed-in session. Implemented by the auth feature
/// and wired in `app/`, so core never imports a feature.
abstract interface class SessionTokens {
  String? get accessToken;

  /// Refreshes the session once (concurrent callers share the call); null when signed out.
  Future<String?> refreshAccessToken();
}

final sessionTokensProvider = Provider<SessionTokens>(
  (ref) => throw UnimplementedError('Overridden in app composition.'),
);
