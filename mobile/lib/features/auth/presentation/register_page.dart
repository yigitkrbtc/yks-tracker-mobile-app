import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../application/auth_providers.dart';
import '../domain/age_policy.dart';
import '../domain/auth_failure.dart';
import '../domain/auth_models.dart';
import '../domain/legal_documents.dart';
import 'auth_routes.dart';
import 'auth_widgets.dart';

class RegisterPage extends ConsumerStatefulWidget {
  const RegisterPage({super.key, this.now});

  /// Fixed clock for tests.
  final DateTime? now;

  @override
  ConsumerState<RegisterPage> createState() => _RegisterPageState();
}

class _RegisterPageState extends ConsumerState<RegisterPage> {
  final _form = GlobalKey<FormState>();
  final _firstName = TextEditingController();
  final _lastName = TextEditingController();
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _termsTap = TapGestureRecognizer();
  final _privacyTap = TapGestureRecognizer();
  int? _birthYear;
  var _accepted = false;
  var _showConsentError = false;
  var _busy = false;

  late final int _currentYear = AgePolicy.currentYear(
    widget.now ?? DateTime.now(),
  );

  @override
  void initState() {
    super.initState();
    _termsTap.onTap = () => context.push(AuthRoutes.legal(LegalDocument.terms));
    _privacyTap.onTap = () =>
        context.push(AuthRoutes.legal(LegalDocument.privacyNotice));
  }

  @override
  void dispose() {
    for (final c in [_firstName, _lastName, _email, _password]) {
      c.dispose();
    }
    _termsTap.dispose();
    _privacyTap.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final valid = _form.currentState!.validate();
    setState(() => _showConsentError = !_accepted);
    if (!valid || !_accepted) {
      return;
    }

    final email = _email.text.trim();
    setState(() => _busy = true);
    try {
      await ref
          .read(authRepositoryProvider)
          .register(
            Registration(
              firstName: _firstName.text.trim(),
              lastName: _lastName.text.trim(),
              email: email,
              password: _password.text,
              birthYear: _birthYear!,
            ),
          );
      if (mounted) {
        context.pushReplacement(AuthRoutes.verify(email));
      }
    } on AuthException catch (e) {
      if (mounted) {
        showAuthError(context, e);
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final latest = AgePolicy.latestAllowedBirthYear(_currentYear);
    final minor =
        _birthYear != null && !AgePolicy.isAdult(_birthYear!, _currentYear);
    final theme = Theme.of(context);

    return AuthScaffold(
      title: 'Kayıt ol',
      children: [
        Form(
          key: _form,
          child: AutofillGroup(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextFormField(
                  controller: _firstName,
                  textCapitalization: TextCapitalization.words,
                  autofillHints: const [AutofillHints.givenName],
                  maxLength: 50,
                  validator: AuthValidators.required,
                  decoration: const InputDecoration(
                    labelText: 'Ad',
                    counterText: '',
                  ),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _lastName,
                  textCapitalization: TextCapitalization.words,
                  autofillHints: const [AutofillHints.familyName],
                  maxLength: 50,
                  validator: AuthValidators.required,
                  decoration: const InputDecoration(
                    labelText: 'Soyad',
                    counterText: '',
                  ),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _email,
                  keyboardType: TextInputType.emailAddress,
                  autofillHints: const [AutofillHints.email],
                  validator: AuthValidators.email,
                  decoration: const InputDecoration(labelText: 'E-posta'),
                ),
                const SizedBox(height: 12),
                PasswordField(
                  controller: _password,
                  validator: AuthValidators.newPassword,
                  autofillHints: const [AutofillHints.newPassword],
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<int>(
                  key: const Key('birthYear'),
                  initialValue: _birthYear,
                  decoration: const InputDecoration(
                    labelText: 'Doğum yılı',
                    helperText: '13 yaşından küçükler kayıt olamaz.',
                  ),
                  items: [
                    for (var y = latest; y >= AgePolicy.earliestBirthYear; y--)
                      DropdownMenuItem(value: y, child: Text('$y')),
                  ],
                  validator: (v) => v == null ? 'Doğum yılını seç.' : null,
                  onChanged: (v) => setState(() => _birthYear = v),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        CheckboxListTile(
          key: const Key('consent'),
          value: _accepted,
          contentPadding: EdgeInsets.zero,
          controlAffinity: ListTileControlAffinity.leading,
          onChanged: (v) => setState(() {
            _accepted = v ?? false;
            _showConsentError = false;
          }),
          title: Text.rich(
            TextSpan(
              children: [
                TextSpan(
                  text: 'Kullanım Koşulları',
                  style: TextStyle(color: theme.colorScheme.primary),
                  recognizer: _termsTap,
                ),
                const TextSpan(text: "'nı okudum, kabul ediyorum; "),
                TextSpan(
                  text: 'Aydınlatma Metni',
                  style: TextStyle(color: theme.colorScheme.primary),
                  recognizer: _privacyTap,
                ),
                const TextSpan(text: "'ni okudum."),
              ],
            ),
          ),
          subtitle: _showConsentError
              ? Text(
                  'Devam etmek için onay gerekli.',
                  style: TextStyle(color: theme.colorScheme.error),
                )
              : null,
        ),
        if (minor)
          TextButton(
            onPressed: () =>
                context.push(AuthRoutes.legal(LegalDocument.parents)),
            child: const Text(
              '18 yaşından küçüksen: veliler için bilgilendirme',
            ),
          ),
        const SizedBox(height: 16),
        BusyButton(label: 'Hesap oluştur', busy: _busy, onPressed: _submit),
      ],
    );
  }
}
