import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../application/auth_providers.dart';
import '../application/session_controller.dart';
import '../domain/auth_failure.dart';
import 'auth_routes.dart';
import 'auth_widgets.dart';

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _form = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  var _busy = false;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) {
      return;
    }
    final email = _email.text.trim();
    setState(() => _busy = true);
    try {
      await ref
          .read(sessionControllerProvider.notifier)
          .login(email, _password.text);
    } on AuthException catch (e) {
      if (!mounted) {
        return;
      }
      switch (e.failure) {
        case AuthFailure.emailNotVerified:
          await _resendAndVerify(email);
        case AuthFailure.deletionPending:
          await _offerRestore(email);
        default:
          showAuthError(context, e);
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  /// A failed resend still opens the verify page: the earlier code may be valid, and the
  /// page can resend again.
  Future<void> _resendAndVerify(String email) async {
    AuthException? resendError;
    try {
      await ref.read(authRepositoryProvider).resendVerificationCode(email);
    } on AuthException catch (e) {
      resendError = e;
    }
    if (!mounted) {
      return;
    }
    if (resendError != null) {
      showAuthError(context, resendError);
    }
    unawaited(context.push(AuthRoutes.verify(email)));
  }

  Future<void> _offerRestore(String email) async {
    final restore = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Hesabın silinmek üzere'),
        content: const Text(
          'Silme talebinden sonraki 30 gün içinde hesabını ve tüm verilerini geri alabilirsin.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Vazgeç'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Hesabı geri al'),
          ),
        ],
      ),
    );
    if (restore != true) {
      return;
    }
    try {
      await ref
          .read(sessionControllerProvider.notifier)
          .restoreAccount(email, _password.text);
    } on AuthException catch (e) {
      if (mounted) {
        showAuthError(context, e);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return AuthScaffold(
      title: 'Giriş yap',
      children: [
        Form(
          key: _form,
          child: AutofillGroup(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextFormField(
                  controller: _email,
                  keyboardType: TextInputType.emailAddress,
                  autofillHints: const [AutofillHints.email],
                  validator: AuthValidators.email,
                  decoration: const InputDecoration(labelText: 'E-posta'),
                ),
                const SizedBox(height: 12),
                PasswordField(controller: _password),
              ],
            ),
          ),
        ),
        const SizedBox(height: 24),
        BusyButton(label: 'Giriş yap', busy: _busy, onPressed: _submit),
        const SizedBox(height: 8),
        TextButton(
          onPressed: () => context.push(AuthRoutes.forgotPassword),
          child: const Text('Şifremi unuttum'),
        ),
        TextButton(
          onPressed: () => context.push(AuthRoutes.register),
          child: const Text('Hesabın yok mu? Kayıt ol'),
        ),
      ],
    );
  }
}
