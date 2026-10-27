/// Text versions the sign-up form shows; must match the API's `Identity:TermsVersion`
/// and `Identity:PrivacyNoticeVersion`. Bump together with the asset text.
abstract final class LegalDocuments {
  static const termsVersion = '1';
  static const privacyNoticeVersion = '1';
}

enum LegalDocument {
  terms('assets/legal/terms.md', 'Kullanım Koşulları'),
  privacyNotice('assets/legal/privacy_notice.md', 'Aydınlatma Metni'),
  parents('assets/legal/parents.md', 'Veliler için bilgilendirme');

  const LegalDocument(this.asset, this.title);

  final String asset;
  final String title;
}
