import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../application/auth_providers.dart';
import '../domain/auth_failure.dart';
import 'auth_routes.dart';
import 'auth_widgets.dart';

class ResetPasswordPage extends ConsumerStatefulWidget {
  const ResetPasswordPage({super.key, required this.email});

  final String email;

  @override
  ConsumerState<ResetPasswordPage> createState() => _ResetPasswordPageState();
}

class _ResetPasswordPageState extends ConsumerState<ResetPasswordPage> {
  final _form = GlobalKey<FormState>();
  final _code = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  var _busy = false;

  @override
  void dispose() {
    _code.dispose();
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) {
      return;
    }
    setState(() => _busy = true);
    try {
      await ref
          .read(authRepositoryProvider)
          .resetPassword(widget.email, _code.text.trim(), _password.text);
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Şifren güncellendi; yeni şifrenle giriş yap.'),
        ),
      );
      context.go(AuthRoutes.login);
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
    return AuthScaffold(
      title: 'Yeni şifre',
      children: [
        Text(
          'Bu adrese kayıtlı bir hesap varsa ${widget.email} adresine 6 haneli bir kod gönderdik.',
        ),
        const SizedBox(height: 16),
        Form(
          key: _form,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextFormField(
                controller: _code,
                keyboardType: TextInputType.number,
                autofillHints: const [AutofillHints.oneTimeCode],
                maxLength: 6,
                validator: AuthValidators.code,
                decoration: const InputDecoration(
                  labelText: 'Kod',
                  counterText: '',
                ),
              ),
              const SizedBox(height: 12),
              PasswordField(
                controller: _password,
                label: 'Yeni şifre',
                validator: AuthValidators.newPassword,
                autofillHints: const [AutofillHints.newPassword],
              ),
              const SizedBox(height: 12),
              PasswordField(
                controller: _confirm,
                label: 'Yeni şifre (tekrar)',
                validator: (v) =>
                    v == _password.text ? null : 'Şifreler eşleşmiyor.',
                autofillHints: const [AutofillHints.newPassword],
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),
        BusyButton(label: 'Şifreyi güncelle', busy: _busy, onPressed: _submit),
      ],
    );
  }
}
