/// Tokens of a completed sign-in. The access token lives only in memory.
class AuthTokens {
  const AuthTokens({
    required this.accessToken,
    required this.accessTokenExpiresAt,
    required this.refreshToken,
  });

  final String accessToken;
  final DateTime accessTokenExpiresAt;
  final String refreshToken;

  bool expiresWithin(Duration margin, DateTime now) =>
      !accessTokenExpiresAt.isAfter(now.add(margin));
}

/// Result of a sign-in step: tokens, or a second factor that only admins get.
sealed class SignInOutcome {
  const SignInOutcome();
}

class SignedIn extends SignInOutcome {
  const SignedIn(this.tokens);

  final AuthTokens tokens;
}

/// Admin accounts must pass TOTP, which the student app does not offer.
class SecondFactorRequired extends SignInOutcome {
  const SecondFactorRequired();
}

class UserProfile {
  const UserProfile({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.birthYear,
    required this.canGiveMarketingConsent,
  });

  final String id;
  final String firstName;
  final String lastName;
  final String email;
  final int birthYear;
  final bool canGiveMarketingConsent;

  Map<String, Object> toJson() => {
    'id': id,
    'firstName': firstName,
    'lastName': lastName,
    'email': email,
    'birthYear': birthYear,
    'canGiveMarketingConsent': canGiveMarketingConsent,
  };

  factory UserProfile.fromJson(Map<String, dynamic> json) => UserProfile(
    id: json['id'] as String,
    firstName: json['firstName'] as String,
    lastName: json['lastName'] as String,
    email: json['email'] as String,
    birthYear: json['birthYear'] as int,
    canGiveMarketingConsent: json['canGiveMarketingConsent'] as bool,
  );
}

class Registration {
  const Registration({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.password,
    required this.birthYear,
  });

  final String firstName;
  final String lastName;
  final String email;
  final String password;
  final int birthYear;
}
