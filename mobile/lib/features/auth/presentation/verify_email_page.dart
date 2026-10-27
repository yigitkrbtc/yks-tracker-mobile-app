import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../application/auth_providers.dart';
import '../application/session_controller.dart';
import '../domain/auth_failure.dart';
import 'auth_widgets.dart';

class VerifyEmailPage extends ConsumerStatefulWidget {
  const VerifyEmailPage({super.key, required this.email});

  final String email;

  @override
  ConsumerState<VerifyEmailPage> createState() => _VerifyEmailPageState();
}

class _VerifyEmailPageState extends ConsumerState<VerifyEmailPage> {
  static const _cooldown = 60;

  final _form = GlobalKey<FormState>();
  final _code = TextEditingController();
  Timer? _timer;
  var _secondsLeft = _cooldown;
  var _busy = false;

  @override
  void initState() {
    super.initState();
    _startCooldown();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _code.dispose();
    super.dispose();
  }

  void _startCooldown() {
    _timer?.cancel();
    setState(() => _secondsLeft = _cooldown);
    _timer = Timer.periodic(const Duration(seconds: 1), (timer) {
      setState(() => _secondsLeft--);
      if (_secondsLeft <= 0) {
        timer.cancel();
      }
    });
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) {
      return;
    }
    setState(() => _busy = true);
    try {
      // Success flips the session; the router then leaves this page.
      await ref
          .read(sessionControllerProvider.notifier)
          .verifyEmail(widget.email, _code.text.trim());
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

  Future<void> _resend() async {
    try {
      await ref
          .read(authRepositoryProvider)
          .resendVerificationCode(widget.email);
      _startCooldown();
    } on AuthException catch (e) {
      if (mounted) {
        showAuthError(context, e);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return AuthScaffold(
      title: 'E-postanı doğrula',
      children: [
        Text(
          '${widget.email} adresine 6 haneli bir kod gönderdik. Kod 10 dakika geçerli.',
        ),
        const SizedBox(height: 16),
        Form(
          key: _form,
          child: TextFormField(
            controller: _code,
            keyboardType: TextInputType.number,
            autofillHints: const [AutofillHints.oneTimeCode],
            maxLength: 6,
            validator: AuthValidators.code,
            decoration: const InputDecoration(
              labelText: 'Doğrulama kodu',
              counterText: '',
            ),
          ),
        ),
        const SizedBox(height: 24),
        BusyButton(label: 'Doğrula', busy: _busy, onPressed: _submit),
        const SizedBox(height: 8),
        TextButton(
          onPressed: _secondsLeft > 0 ? null : _resend,
          child: Text(
            _secondsLeft > 0
                ? 'Kodu tekrar gönder ($_secondsLeft sn)'
                : 'Kodu tekrar gönder',
          ),
        ),
      ],
    );
  }
}
