import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../application/session_controller.dart';
import '../domain/auth_failure.dart';
import 'auth_widgets.dart';

class DeleteAccountPage extends ConsumerStatefulWidget {
  const DeleteAccountPage({super.key});

  @override
  ConsumerState<DeleteAccountPage> createState() => _DeleteAccountPageState();
}

class _DeleteAccountPageState extends ConsumerState<DeleteAccountPage> {
  final _form = GlobalKey<FormState>();
  final _password = TextEditingController();
  var _busy = false;

  @override
  void dispose() {
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) {
      return;
    }
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Hesabın silinsin mi?'),
        content: const Text(
          'Tüm cihazlarda oturumun kapanır. 30 gün içinde giriş yapıp geri almazsan hesabın ve tüm verilerin kalıcı olarak silinir.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Vazgeç'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(context).colorScheme.error,
            ),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Hesabımı sil'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) {
      return;
    }

    final messenger = ScaffoldMessenger.of(context);
    setState(() => _busy = true);
    try {
      final purgeAt = await ref
          .read(sessionControllerProvider.notifier)
          .deleteAccount(_password.text);
      final day = purgeAt.toLocal();
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            'Hesabın ${day.day}.${day.month}.${day.year} tarihinde silinecek. O zamana kadar giriş yapıp geri alabilirsin.',
          ),
        ),
      );
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
      title: 'Hesabımı sil',
      children: [
        const Text(
          'Hesabın hemen pasifleşir ve tüm cihazlarda oturumun kapanır. '
          '30 gün içinde aynı e-posta ve şifreyle giriş yaparsan hesabını geri alabilirsin; '
          'süre dolunca hesabın ve tüm verilerin kalıcı olarak silinir.',
        ),
        const SizedBox(height: 16),
        Form(
          key: _form,
          child: PasswordField(controller: _password, label: 'Şifren'),
        ),
        const SizedBox(height: 24),
        BusyButton(label: 'Hesabımı sil', busy: _busy, onPressed: _submit),
      ],
    );
  }
}
