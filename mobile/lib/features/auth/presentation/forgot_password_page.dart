import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../application/auth_providers.dart';
import '../domain/auth_failure.dart';
import 'auth_routes.dart';
import 'auth_widgets.dart';

class ForgotPasswordPage extends ConsumerStatefulWidget {
  const ForgotPasswordPage({super.key});

  @override
  ConsumerState<ForgotPasswordPage> createState() => _ForgotPasswordPageState();
}

class _ForgotPasswordPageState extends ConsumerState<ForgotPasswordPage> {
  final _form = GlobalKey<FormState>();
  final _email = TextEditingController();
  var _busy = false;

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) {
      return;
    }
    final email = _email.text.trim();
    setState(() => _busy = true);
    try {
      await ref.read(authRepositoryProvider).forgotPassword(email);
      if (mounted) {
        context.pushReplacement(AuthRoutes.resetPassword(email));
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
    return AuthScaffold(
      title: 'Şifremi unuttum',
      children: [
        const Text(
          'Hesabının e-posta adresini gir; şifreni sıfırlaman için bir kod gönderelim.',
        ),
        const SizedBox(height: 16),
        Form(
          key: _form,
          child: TextFormField(
            controller: _email,
            keyboardType: TextInputType.emailAddress,
            autofillHints: const [AutofillHints.email],
            validator: AuthValidators.email,
            decoration: const InputDecoration(labelText: 'E-posta'),
          ),
        ),
        const SizedBox(height: 24),
        BusyButton(label: 'Kod gönder', busy: _busy, onPressed: _submit),
      ],
    );
  }
}
