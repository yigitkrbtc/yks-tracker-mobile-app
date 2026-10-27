/// Failures the auth screens react to; mapped from the API's problem `type`.
enum AuthFailure {
  invalidCredentials('E-posta veya şifre hatalı.'),
  emailNotVerified('E-posta adresin henüz doğrulanmadı.'),
  deletionPending('Hesabın silinmek üzere.'),
  invalidCode('Kod hatalı.'),
  codeExpired('Kodun süresi doldu; yeni kod iste.'),
  underage('Kayıt için en az 13 yaşında olmalısın.'),
  outdatedTerms('Koşullar güncellendi; uygulamayı güncelleyip tekrar dene.'),
  invalidPassword('Şifre hatalı.'),
  sessionExpired('Oturumun sona erdi; tekrar giriş yap.'),
  secondFactorRequired('Yönetici hesapları yönetim panelinden giriş yapar.'),
  validation('Bilgileri kontrol et.'),
  tooManyRequests('Çok fazla deneme yaptın; biraz sonra tekrar dene.'),
  network('Sunucuya ulaşılamadı; bağlantını kontrol et.'),
  unknown('Bir şeyler ters gitti; tekrar dene.');

  const AuthFailure(this.message);

  final String message;

  static AuthFailure fromProblemType(String? type, int status) =>
      switch (type) {
        'identity.invalid_credentials' => invalidCredentials,
        'identity.email_not_verified' => emailNotVerified,
        'identity.deletion_pending' => deletionPending,
        'identity.invalid_code' => invalidCode,
        'identity.code_expired' => codeExpired,
        'identity.underage' => underage,
        'identity.outdated_terms' => outdatedTerms,
        'identity.invalid_password' => invalidPassword,
        'identity.invalid_refresh_token' => sessionExpired,
        _ when status == 429 => tooManyRequests,
        _ when status == 400 => validation,
        _ when status == 0 => network,
        _ => unknown,
      };
}

class AuthException implements Exception {
  const AuthException(this.failure, {this.fieldErrors = const {}});

  final AuthFailure failure;

  /// Server-side field messages (camelCase keys), for validation failures.
  final Map<String, List<String>> fieldErrors;

  String get message => fieldErrors.isEmpty
      ? failure.message
      : fieldErrors.values.expand((m) => m).join('\n');

  @override
  String toString() => 'AuthException($failure)';
}
