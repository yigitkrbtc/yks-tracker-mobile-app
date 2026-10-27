import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/design/gallery/design_gallery_page.dart';
import '../features/auth/application/session_controller.dart';
import '../features/auth/presentation/auth_routes.dart';
import '../features/home/presentation/home_page.dart';

const _splash = '/splash';

final routerProvider = Provider<GoRouter>((ref) {
  final sessionChanges = ValueNotifier<int>(0);
  ref.listen(sessionControllerProvider, (_, _) => sessionChanges.value++);
  ref.onDispose(sessionChanges.dispose);

  final router = GoRouter(
    initialLocation: _splash,
    refreshListenable: sessionChanges,
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
      return location == _splash || AuthRoutes.isSignedOutOnly(location)
          ? '/'
          : null;
    },
    routes: [
      GoRoute(path: _splash, builder: (context, state) => const _SplashPage()),
      GoRoute(path: '/', builder: (context, state) => const HomePage()),
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
