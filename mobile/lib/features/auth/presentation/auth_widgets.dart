import 'package:flutter/material.dart';

import '../domain/auth_failure.dart';

/// Centered, scrollable, width-capped form shell shared by the auth screens.
class AuthScaffold extends StatelessWidget {
  const AuthScaffold({super.key, required this.title, required this.children});

  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: children,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class PasswordField extends StatefulWidget {
  const PasswordField({
    super.key,
    required this.controller,
    this.label = 'Şifre',
    this.validator,
    this.autofillHints = const [AutofillHints.password],
  });

  final TextEditingController controller;
  final String label;
  final FormFieldValidator<String>? validator;
  final Iterable<String> autofillHints;

  @override
  State<PasswordField> createState() => _PasswordFieldState();
}

class _PasswordFieldState extends State<PasswordField> {
  var _obscured = true;

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: widget.controller,
      obscureText: _obscured,
      autofillHints: widget.autofillHints,
      validator: widget.validator ?? AuthValidators.required,
      decoration: InputDecoration(
        labelText: widget.label,
        suffixIcon: IconButton(
          tooltip: _obscured ? 'Şifreyi göster' : 'Şifreyi gizle',
          icon: Icon(_obscured ? Icons.visibility : Icons.visibility_off),
          onPressed: () => setState(() => _obscured = !_obscured),
        ),
      ),
    );
  }
}

/// Client-side checks mirror the API's rules so most mistakes never reach the server.
abstract final class AuthValidators {
  static final _email = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

  static String? required(String? value) =>
      (value == null || value.trim().isEmpty) ? 'Bu alan zorunlu.' : null;

  static String? email(String? value) =>
      required(value) ??
      (_email.hasMatch(value!.trim()) ? null : 'Geçerli bir e-posta gir.');

  static String? newPassword(String? value) {
    final missing = required(value);
    if (missing != null) {
      return missing;
    }
    if (value!.length < 10) {
      return 'Şifre en az 10 karakter olmalı.';
    }
    if (!RegExp(r'\p{L}', unicode: true).hasMatch(value) ||
        !RegExp(r'\d').hasMatch(value)) {
      return 'Şifre en az bir harf ve bir rakam içermeli.';
    }
    return null;
  }

  static String? code(String? value) =>
      RegExp(r'^\d{6}$').hasMatch(value?.trim() ?? '')
      ? null
      : '6 haneli kodu gir.';
}

/// Shows an auth error the way every screen does: a snackbar with the server's or our message.
void showAuthError(BuildContext context, Object error) {
  final message = error is AuthException
      ? error.message
      : AuthFailure.unknown.message;
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}

class BusyButton extends StatelessWidget {
  const BusyButton({
    super.key,
    required this.label,
    required this.busy,
    required this.onPressed,
  });

  final String label;
  final bool busy;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      onPressed: busy ? null : onPressed,
      child: busy
          ? const SizedBox.square(
              dimension: 20,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          : Text(label),
    );
  }
}
