import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/design/gallery/design_gallery_page.dart';
import '../features/auth/application/session_controller.dart';
import '../features/auth/presentation/auth_routes.dart';
import '../features/mock_exams/presentation/mock_exam_form_page.dart';
import '../features/mock_exams/presentation/mock_exam_marks_page.dart';
import '../features/profile/application/profile_providers.dart';
import '../features/profile/presentation/profile_page.dart';
import '../features/topic_tests/presentation/topic_test_form_page.dart';
import 'home_shell.dart';

const _splash = '/splash';

final routerProvider = Provider<GoRouter>((ref) {
  final changes = ValueNotifier<int>(0);
  ref.listen(sessionControllerProvider, (_, _) => changes.value++);
  ref.listen(studyProfileProvider, (_, _) => changes.value++);
  ref.onDispose(changes.dispose);

  final router = GoRouter(
    initialLocation: _splash,
    refreshListenable: changes,
    redirect: (context, state) {
      final session = ref.read(sessionControllerProvider);
      final location = state.matchedLocation;
      if (session.isLoading && !session.hasValue) {
        return location == _splash ? null : _splash;
      }

      final signedIn = session.value is SignedInState;
      if (!signedIn) {
        return AuthRoutes.isPublic(location) ? null : AuthRoutes.login;
      }

      // Signed in: the study profile decides between onboarding and the app. If it cannot be loaded
      // (offline), the app opens anyway and the topic list offers a retry.
      // A reload (e.g. sign-in after sign-out) still carries the previous user's value; never decide on it.
      final profile = ref.read(studyProfileProvider);
      if (profile.isLoading) {
        return location == _splash ? null : _splash;
      }
      final needsOnboarding = !profile.hasError && profile.value == null;
      if (needsOnboarding) {
        return location == ProfilePage.onboardingPath
            ? null
            : ProfilePage.onboardingPath;
      }
      return location == _splash ||
              location == ProfilePage.onboardingPath ||
              AuthRoutes.isSignedOutOnly(location)
          ? '/'
          : null;
    },
    routes: [
      GoRoute(path: _splash, builder: (context, state) => const _SplashPage()),
      GoRoute(path: '/', builder: (context, state) => const HomeShell()),
      GoRoute(
        path: ProfilePage.onboardingPath,
        builder: (context, state) => const ProfilePage(onboarding: true),
      ),
      GoRoute(
        path: ProfilePage.editPath,
        builder: (context, state) => const ProfilePage(),
      ),
      GoRoute(
        path: TopicTestFormPage.newPath,
        builder: (context, state) =>
            TopicTestFormPage(topicId: state.uri.queryParameters['topic']),
      ),
      GoRoute(
        path: TopicTestFormPage.editPattern,
        builder: (context, state) =>
            TopicTestFormPage(id: state.pathParameters['id']),
      ),
      GoRoute(
        path: MockExamFormPage.newPath,
        builder: (context, state) => const MockExamFormPage(),
      ),
      GoRoute(
        path: MockExamMarksPage.pattern,
        builder: (context, state) =>
            MockExamMarksPage(id: state.pathParameters['id']!),
      ),
      GoRoute(
        path: MockExamFormPage.editPattern,
        builder: (context, state) =>
            MockExamFormPage(id: state.pathParameters['id']),
      ),
      ...AuthRoutes.routes,
      GoRoute(
        path: DesignGalleryPage.path,
        builder: (context, state) => const DesignGalleryPage(),
      ),
    ],
  );
  ref.onDispose(router.dispose);
  return router;
});

class _SplashPage extends StatelessWidget {
  const _SplashPage();

  @override
  Widget build(BuildContext context) =>
      const Scaffold(body: Center(child: CircularProgressIndicator()));
}
