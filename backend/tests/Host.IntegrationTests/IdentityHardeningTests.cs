using System.Net;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Regressions for the 2026-11 security audit (ADR-0021).</summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class IdentityHardeningTests(NirengiApiFactory factory)
{
    private const string AttackerPassword = "saldirganSifre99";

    private readonly AuthApi _api = new(factory);

    /// <summary>G1: a second sign-up must not hand the victim's pending account to whoever chose the new password.</summary>
    [Fact]
    public async Task Re_registration_cannot_take_over_a_pending_account()
    {
        var email = AuthApi.NewEmail();
        (await _api.Post("/v1/auth/register", AuthApi.Registration(email))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var victimCode = factory.Mail.LatestCode(email);

        // Inside the resend cooldown: no new code goes out, the victim's code stays the one in the mailbox.
        (await _api.Post("/v1/auth/register", AuthApi.Registration(email, password: AttackerPassword))).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var verified = await _api.Post("/v1/auth/email/verify", new { email, code = victimCode, password = AuthApi.Password });
        await AuthApi.ShouldBeProblem(verified, HttpStatusCode.Unauthorized, "identity.invalid_credentials");
        (await _api.Post("/v1/auth/login", new { email, password = AttackerPassword })).StatusCode.ShouldNotBe(HttpStatusCode.OK);

        // The mailbox owner signs up again and keeps the account with their own password.
        await _api.RegisterAndVerify(email);
        (await _api.Post("/v1/auth/login", new { email, password = AuthApi.Password })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.Post("/v1/auth/login", new { email, password = AttackerPassword })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>M3: a rotation whose answer never arrived can be retried; the lost successor is the one rotated.</summary>
    [Fact]
    public async Task A_retried_refresh_inside_the_grace_period_keeps_the_session()
    {
        var first = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var lost = await AuthApi.Read<AuthApi.Tokens>(await _api.Post("/v1/auth/refresh", new { refreshToken = first.RefreshToken }));

        var retried = await _api.Post("/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        retried.StatusCode.ShouldBe(HttpStatusCode.OK);
        var current = await AuthApi.Read<AuthApi.Tokens>(retried);
        current.RefreshToken.ShouldNotBe(lost.RefreshToken);

        // The answer that went missing is spent; presenting it now is reuse and ends the session.
        (await _api.Post("/v1/auth/refresh", new { refreshToken = lost.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _api.Post("/v1/auth/refresh", new { refreshToken = current.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>G1 corollary: the password is checked before the code, and a wrong one counts toward the lockout.</summary>
    [Fact]
    public async Task Verification_with_a_wrong_password_is_refused()
    {
        var email = AuthApi.NewEmail();
        await _api.Post("/v1/auth/register", AuthApi.Registration(email));

        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/email/verify", new { email, code = factory.Mail.LatestCode(email), password = AttackerPassword }),
            HttpStatusCode.Unauthorized,
            "identity.invalid_credentials");
    }

    /// <summary>İ4: a common word plus digits is refused at sign-up.</summary>
    [Theory]
    [InlineData("Galatasaray1905")]
    [InlineData("sifre123456")]
    [InlineData("1q2w3e4r5t")]
    [InlineData("Ada12345678")]
    public async Task Common_passwords_are_refused(string password)
    {
        var response = await _api.Post("/v1/auth/register", AuthApi.Registration(AuthApi.NewEmail(), password: password));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
