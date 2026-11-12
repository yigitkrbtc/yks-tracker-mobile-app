using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OtpNet;
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

    /// <summary>M3: a response lost twice in a row still leaves the session alive (the chain of superseded tokens is followed).</summary>
    [Fact]
    public async Task A_refresh_lost_twice_keeps_the_session()
    {
        var first = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var lost1 = await AuthApi.Read<AuthApi.Tokens>(await _api.Post("/v1/auth/refresh", new { refreshToken = first.RefreshToken }));
        var lost2 = await AuthApi.Read<AuthApi.Tokens>(await _api.Post("/v1/auth/refresh", new { refreshToken = first.RefreshToken }));

        var third = await _api.Post("/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        third.StatusCode.ShouldBe(HttpStatusCode.OK);
        var current = await AuthApi.Read<AuthApi.Tokens>(third);
        current.RefreshToken.ShouldNotBe(lost1.RefreshToken);
        current.RefreshToken.ShouldNotBe(lost2.RefreshToken);

        // The token that did arrive keeps working; the lost ones are reuse.
        var next = await _api.Post("/v1/auth/refresh", new { refreshToken = current.RefreshToken });
        next.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.Post("/v1/auth/refresh", new { refreshToken = lost1.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _api.Post("/v1/auth/refresh", new { refreshToken = (await AuthApi.Read<AuthApi.Tokens>(next)).RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>M3: once the successor was presented and rotated, replaying the old token is theft, not a lost answer.</summary>
    [Fact]
    public async Task Replaying_a_token_whose_successor_was_used_ends_the_session()
    {
        var first = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var second = await AuthApi.Read<AuthApi.Tokens>(await _api.Post("/v1/auth/refresh", new { refreshToken = first.RefreshToken }));
        var third = await AuthApi.Read<AuthApi.Tokens>(await _api.Post("/v1/auth/refresh", new { refreshToken = second.RefreshToken }));

        (await _api.Post("/v1/auth/refresh", new { refreshToken = first.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _api.Post("/v1/auth/refresh", new { refreshToken = third.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>G4: asking for codes is not capped, so someone hammering "forgot" cannot keep the owner from getting one.</summary>
    [Fact]
    public async Task Flooding_forgot_password_does_not_keep_the_owner_from_a_code()
    {
        var email = AuthApi.NewEmail();
        await _api.RegisterAndVerify(email);
        using var host = factory.WithWebHostBuilder(b => b.UseSetting("Identity:CodeResendCooldown", "00:00:00"));
        var client = host.CreateClient();

        for (var i = 0; i < 6; i++)
        {
            (await Post(client, "/v1/auth/password/forgot", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        factory.Mail.To(email).Count(m => m.Subject.Any(char.IsDigit)).ShouldBe(7); // verification code + six reset codes
        const string newPassword = "yeniSifre456";
        (await Post(client, "/v1/auth/password/reset", new { email, code = factory.Mail.LatestCode(email), newPassword }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>G4: guesses are budgeted per day across codes; asking for a fresh code does not buy new ones.</summary>
    [Fact]
    public async Task Failed_guesses_are_budgeted_across_fresh_codes()
    {
        var email = AuthApi.NewEmail();
        await _api.RegisterAndVerify(email);
        using var host = factory.WithWebHostBuilder(b => b.UseSetting("Identity:CodeResendCooldown", "00:00:00"));
        var client = host.CreateClient();
        const string newPassword = "yeniSifre456";

        // 5 codes x 5 wrong guesses = the whole day's budget.
        for (var round = 0; round < 5; round++)
        {
            (await Post(client, "/v1/auth/password/forgot", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
            var real = factory.Mail.LatestCode(email);
            var wrong = real == "000000" ? "000001" : "000000";
            for (var guess = 0; guess < 5; guess++)
            {
                (await Post(client, "/v1/auth/password/reset", new { email, code = wrong, newPassword })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            }
        }

        (await Post(client, "/v1/auth/password/forgot", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await AuthApi.ShouldBeProblem(
            await Post(client, "/v1/auth/password/reset", new { email, code = factory.Mail.LatestCode(email), newPassword }),
            HttpStatusCode.BadRequest,
            "identity.code_expired");
    }

    /// <summary>
    /// G8: an account promoted to admin by configuration cannot turn a student refresh token into an admin session
    /// without the second factor.
    /// </summary>
    [Fact]
    public async Task A_student_session_does_not_become_an_admin_session_when_the_address_is_added_to_admins()
    {
        var email = AuthApi.NewEmail();
        var student = await _api.RegisterAndVerify(email);
        using var host = factory.WithWebHostBuilder(b => b.UseSetting("Identity:AdminEmails:1", email));
        var client = host.CreateClient();

        var login = await AuthApi.Read<AuthApi.SignIn>(await Post(client, "/v1/auth/login", new { email, password = AuthApi.Password }));
        login.Tokens.ShouldBeNull();
        login.Mfa!.Step.ShouldBe("TotpSetup");

        var refreshed = await Post(client, "/v1/auth/refresh", new { refreshToken = student.RefreshToken });
        await AuthApi.ShouldBeProblem(refreshed, HttpStatusCode.Unauthorized, "identity.invalid_refresh_token");
        (await Post(client, "/v1/auth/refresh", new { refreshToken = student.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>G8: an admin removed from the list loses the session at the next refresh and signs in as a student.</summary>
    [Fact]
    public async Task An_admin_removed_from_the_list_is_signed_out_and_returns_as_a_student()
    {
        var email = AuthApi.NewEmail();
        using var adminHost = factory.WithWebHostBuilder(b => b.UseSetting("Identity:AdminEmails:1", email));
        var adminClient = adminHost.CreateClient();

        (await Post(adminClient, "/v1/auth/register", AuthApi.Registration(email))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var verified = await AuthApi.Read<AuthApi.SignIn>(
            await Post(adminClient, "/v1/auth/email/verify", new { email, code = factory.Mail.LatestCode(email), password = AuthApi.Password }));
        verified.Mfa!.Step.ShouldBe("TotpSetup");
        var setup = await AuthApi.Read<TotpSetup>(await Post(adminClient, "/v1/auth/mfa/totp/setup", new { mfaToken = verified.Mfa.MfaToken }));
        var code = new Totp(Base32Encoding.ToBytes(setup.Secret)).ComputeTotp();
        var enrolled = await AuthApi.Read<TotpEnrollment>(
            await Post(adminClient, "/v1/auth/mfa/totp/setup/confirm", new { mfaToken = verified.Mfa.MfaToken, code }));

        // The session passed the second factor, so it rotates; the flag follows the family.
        var rotated = await Post(adminClient, "/v1/auth/refresh", new { refreshToken = enrolled.Tokens.RefreshToken });
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var current = await AuthApi.Read<AuthApi.Tokens>(rotated);

        // The default host no longer lists the address.
        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/refresh", new { refreshToken = current.RefreshToken }),
            HttpStatusCode.Unauthorized,
            "identity.invalid_refresh_token");
        var login = await AuthApi.Read<AuthApi.SignIn>(await _api.Post("/v1/auth/login", new { email, password = AuthApi.Password }));
        login.Mfa.ShouldBeNull();
        (await AuthApi.Read<AuthApi.Profile>(await _api.GetMe(login.Tokens!.AccessToken))).Role.ShouldBe("Student");
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string path, object body) =>
        client.PostAsJsonAsync(path, body, AuthApi.Json, TestContext.Current.CancellationToken);

    private sealed record TotpSetup(string Secret, string OtpAuthUri);

    private sealed record TotpEnrollment(AuthApi.Tokens Tokens, IReadOnlyList<string> RecoveryCodes);
}
