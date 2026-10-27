import 'package:go_router/go_router.dart';

import '../domain/legal_documents.dart';
import 'delete_account_page.dart';
import 'forgot_password_page.dart';
import 'legal_document_page.dart';
import 'login_page.dart';
import 'register_page.dart';
import 'reset_password_page.dart';
import 'verify_email_page.dart';

abstract final class AuthRoutes {
  static const login = '/login';
  static const register = '/register';
  static const forgotPassword = '/forgot-password';
  static const deleteAccount = '/account/delete';

  static String verify(String email) =>
      Uri(path: '/verify', queryParameters: {'email': email}).toString();

  static String resetPassword(String email) => Uri(
    path: '/reset-password',
    queryParameters: {'email': email},
  ).toString();

  static String legal(LegalDocument document) => '/legal/${document.name}';

  /// Reachable without a session; signed-in users are sent home from these (except legal texts).
  static bool isSignedOutOnly(String location) => const {
    login,
    register,
    forgotPassword,
    '/verify',
    '/reset-password',
  }.contains(location);

  static bool isPublic(String location) =>
      isSignedOutOnly(location) || location.startsWith('/legal/');

  static List<RouteBase> get routes => [
    GoRoute(path: login, builder: (context, state) => const LoginPage()),
    GoRoute(path: register, builder: (context, state) => const RegisterPage()),
    GoRoute(
      path: '/verify',
      builder: (context, state) =>
          VerifyEmailPage(email: state.uri.queryParameters['email'] ?? ''),
    ),
    GoRoute(
      path: forgotPassword,
      builder: (context, state) => const ForgotPasswordPage(),
    ),
    GoRoute(
      path: '/reset-password',
      builder: (context, state) =>
          ResetPasswordPage(email: state.uri.queryParameters['email'] ?? ''),
    ),
    GoRoute(
      path: '/legal/:document',
      builder: (context, state) => LegalDocumentPage(
        document: LegalDocument.values.byName(
          state.pathParameters['document']!,
        ),
      ),
    ),
    GoRoute(
      path: deleteAccount,
      builder: (context, state) => const DeleteAccountPage(),
    ),
  ];
}
