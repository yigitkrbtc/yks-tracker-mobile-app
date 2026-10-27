using System.Net;
using OtpNet;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class AdminMfaTests(NirengiApiFactory factory)
{
    private readonly AuthApi _api = new(factory);

    // One admin address per host, so the whole lifecycle runs as one ordered scenario.
    [Fact]
    public async Task Admin_must_enrol_totp_then_sign_in_with_a_second_factor()
    {
        const string email = NirengiApiFactory.AdminEmail;
        (await _api.Post("/v1/auth/register", AuthApi.Registration(email))).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Verification proves the mailbox but an admin still gets no tokens without TOTP.
        var verified = await AuthApi.Read<AuthApi.SignIn>(
            await _api.Post("/v1/auth/email/verify", new { email, code = factory.Mail.LatestCode(email) }));
        verified.Tokens.ShouldBeNull();
        verified.Mfa!.Step.ShouldBe("TotpSetup");
        var setupToken = verified.Mfa.MfaToken;

        (await _api.GetMe(setupToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/mfa/totp", new { mfaToken = setupToken, code = "123456" }),
            HttpStatusCode.Unauthorized,
            "identity.invalid_mfa_token");

        var setup = await AuthApi.Read<Setup>(await _api.Post("/v1/auth/mfa/totp/setup", new { mfaToken = setupToken }));
        setup.OtpAuthUri.ShouldStartWith("otpauth://totp/");
        var totp = new Totp(Base32Encoding.ToBytes(setup.Secret));

        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/mfa/totp/setup/confirm", new { mfaToken = setupToken, code = WrongCode(totp) }),
            HttpStatusCode.BadRequest,
            "identity.invalid_totp_code");

        var currentCode = totp.ComputeTotp();
        var enrolled = await _api.Post("/v1/auth/mfa/totp/setup/confirm", new { mfaToken = setupToken, code = currentCode });
        enrolled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var enrollment = await AuthApi.Read<Enrollment>(enrolled);
        enrollment.RecoveryCodes.Count.ShouldBe(10);
        (await AuthApi.Read<AuthApi.Profile>(await _api.GetMe(enrollment.Tokens.AccessToken))).Role.ShouldBe("Admin");

        // Next sign-in asks for the code; the code already used for enrolment is refused (no replay).
        var login = await AuthApi.Read<AuthApi.SignIn>(await _api.Post("/v1/auth/login", new { email, password = AuthApi.Password }));
        login.Tokens.ShouldBeNull();
        login.Mfa!.Step.ShouldBe("Totp");
        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/mfa/totp", new { mfaToken = login.Mfa.MfaToken, code = currentCode }),
            HttpStatusCode.BadRequest,
            "identity.invalid_totp_code");

        var recovery = enrollment.RecoveryCodes[0].ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);
        var signedIn = await _api.Post("/v1/auth/mfa/totp", new { mfaToken = login.Mfa.MfaToken, recoveryCode = recovery });
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AuthApi.Read<AuthApi.Tokens>(signedIn)).AccessToken.ShouldNotBeNullOrEmpty();

        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/mfa/totp", new { mfaToken = login.Mfa.MfaToken, recoveryCode = recovery }),
            HttpStatusCode.BadRequest,
            "identity.invalid_totp_code");
        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/mfa/totp/setup", new { mfaToken = login.Mfa.MfaToken }),
            HttpStatusCode.Unauthorized,
            "identity.invalid_mfa_token");
    }

    private static string WrongCode(Totp totp)
    {
        var now = DateTime.UtcNow;
        var valid = new[] { totp.ComputeTotp(now.AddSeconds(-30)), totp.ComputeTotp(now), totp.ComputeTotp(now.AddSeconds(30)) };
        return Enumerable.Range(0, 10).Select(i => new string((char)('0' + i), 6)).First(c => !valid.Contains(c));
    }

    private sealed record Setup(string Secret, string OtpAuthUri);

    private sealed record Enrollment(AuthApi.Tokens Tokens, IReadOnlyList<string> RecoveryCodes);
}
