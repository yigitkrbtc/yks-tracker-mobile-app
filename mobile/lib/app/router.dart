import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/design/gallery/design_gallery_page.dart';
import '../features/analytics/presentation/topic_analysis_page.dart';
import '../features/auth/application/session_controller.dart';
import '../features/auth/presentation/auth_routes.dart';
import '../features/exam_calendar/presentation/exam_calendar_widgets.dart';
import '../features/goals/presentation/goal_settings_page.dart';
import '../features/mock_exams/presentation/mock_exam_form_page.dart';
import '../features/mock_exams/presentation/mock_exam_marks_page.dart';
import '../features/profile/application/profile_providers.dart';
import '../features/profile/presentation/profile_page.dart';
import '../features/recommendations/presentation/plan_widgets.dart';
import '../features/topic_tests/presentation/topic_test_form_page.dart';
import 'home_shell.dart';

const _splash = '/splash';

final routerProvider = Provider<GoRouter>((ref) {
  final changes = ValueNotifier<int>(0);
  ref.listen(sessionControllerProvider, (_, _) => changes.value++);
  // Redirects look only at whether the profile is loading, failed or missing. Refreshing on every saved edit
  // would race a page that pops right after saving and put it back.
  ref.listen(studyProfileProvider, (previous, next) {
    if (previous == null ||
        previous.isLoading != next.isLoading ||
        previous.hasError != next.hasError ||
        (previous.value == null) != (next.value == null)) {
      changes.value++;
    }
  });
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
      GoRoute(
        path: TopicAnalysisPage.pattern,
        builder: (context, state) => TopicAnalysisPage(
          topicId: state.pathParameters['id']!,
          onRecordTest: (id) => context.push(TopicTestFormPage.newForTopic(id)),
        ),
      ),
      GoRoute(
        path: WeeklyPlanPage.path,
        builder: (context, state) =>
            WeeklyPlanPage(actions: planActionsFor(context)),
      ),
      GoRoute(
        path: ExamCalendarPage.path,
        builder: (context, state) => const ExamCalendarPage(),
      ),
      GoRoute(
        path: GoalSettingsPage.path,
        builder: (context, state) => const GoalSettingsPage(),
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
