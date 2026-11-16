using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.Modules.Identity.Domain.Codes;
using Nirengi.Modules.Identity.Infrastructure;
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
        var victimCode = await factory.Mail.WaitForCode(email);

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
            await _api.Post("/v1/auth/email/verify", new { email, code = await factory.Mail.WaitForCode(email), password = AttackerPassword }),
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

        // Mails go out through the outbox; one for a code that a later request already replaced is not sent.
        await factory.WaitForIdentityMailDelivered();
        factory.Mail.To(email).Count(m => m.Subject.Any(char.IsDigit)).ShouldBeInRange(2, 7); // verification code + the reset codes still current
        const string newPassword = "yeniSifre456";
        (await Post(client, "/v1/auth/password/reset", new { email, code = factory.Mail.LatestCode(email), newPassword }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// G4: the wait before a new code doubles after three codes in an hour, and a request inside the wait looks the same as
    /// one that sent a code (no 429, no difference an outsider could use to learn about the account), but sends nothing.
    /// </summary>
    [Fact]
    public async Task The_wait_between_reset_codes_grows_within_an_hour_without_changing_the_answer()
    {
        var email = AuthApi.NewEmail();
        await _api.RegisterAndVerify(email);
        var clock = new SettableClock(DateTimeOffset.UtcNow);
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services => services.AddSingleton<IClock>(clock)));
        var client = host.CreateClient();

        async Task<(HttpStatusCode Status, string Hash)> Forgot()
        {
            var response = await Post(client, "/v1/auth/password/forgot", new { email });
            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var userId = await db.Users.Where(u => u.NormalizedEmail == Nirengi.Modules.Identity.Domain.Users.Email.Normalize(email)).Select(u => u.Id).SingleAsync(TestContext.Current.CancellationToken);
            var row = await db.VerificationCodes.AsNoTracking().SingleAsync(c => c.UserId == userId && c.Purpose == CodePurpose.PasswordReset, TestContext.Current.CancellationToken);
            return (response.StatusCode, row.CodeHash);
        }

        var (status, hash) = await Forgot();
        status.ShouldBe(HttpStatusCode.Accepted);

        // Codes 1-3 are 60 s apart, then 120, 240: a request one second early is answered the same way but sends nothing.
        foreach (var wait in new[] { 60, 60, 60, 120, 240 })
        {
            clock.UtcNow += TimeSpan.FromSeconds(wait - 1);
            var early = await Forgot();
            early.Status.ShouldBe(HttpStatusCode.Accepted);
            early.Hash.ShouldBe(hash, $"a code went out {wait - 1} s after the last one");

            clock.UtcNow += TimeSpan.FromSeconds(1);
            var due = await Forgot();
            due.Status.ShouldBe(HttpStatusCode.Accepted);
            due.Hash.ShouldNotBe(hash, $"no code went out {wait} s after the last one");
            hash = due.Hash;
        }

        // The owner is never locked out: an hour later the window is over and the wait is short again.
        clock.UtcNow += TimeSpan.FromHours(1);
        (await Forgot()).Hash.ShouldNotBe(hash);
    }

    /// <summary>
    /// G4: a code dies after 5 wrong guesses, even for the right code afterwards; a fresh code works at once, because
    /// there is no account-level budget an attacker could burn to lock the owner out.
    /// </summary>
    [Fact]
    public async Task A_code_dies_after_five_wrong_guesses_and_a_fresh_one_still_works()
    {
        var email = AuthApi.NewEmail();
        await _api.RegisterAndVerify(email);
        using var host = factory.WithWebHostBuilder(b => b.UseSetting("Identity:CodeResendCooldown", "00:00:00"));
        var client = host.CreateClient();
        const string newPassword = "yeniSifre456";

        // Burn several codes in a row; each one is spent after its own 5 guesses.
        for (var round = 0; round < 3; round++)
        {
            var mailsBefore = factory.Mail.To(email).Count;
            (await Post(client, "/v1/auth/password/forgot", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
            var real = await factory.Mail.WaitForCode(email, mailsBefore);
            var wrong = real == "00000000" ? "00000001" : "00000000";
            for (var guess = 0; guess < 4; guess++)
            {
                await AuthApi.ShouldBeProblem(
                    await Post(client, "/v1/auth/password/reset", new { email, code = wrong, newPassword }),
                    HttpStatusCode.BadRequest,
                    "identity.invalid_code");
            }

            await AuthApi.ShouldBeProblem(
                await Post(client, "/v1/auth/password/reset", new { email, code = wrong, newPassword }),
                HttpStatusCode.BadRequest,
                "identity.code_expired");

            // The right code no longer works on the dead one.
            await AuthApi.ShouldBeProblem(
                await Post(client, "/v1/auth/password/reset", new { email, code = real, newPassword }),
                HttpStatusCode.BadRequest,
                "identity.code_expired");
        }

        var lastMailsBefore = factory.Mail.To(email).Count;
        (await Post(client, "/v1/auth/password/forgot", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await Post(client, "/v1/auth/password/reset", new { email, code = await factory.Mail.WaitForCode(email, lastMailsBefore), newPassword }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
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
            await Post(adminClient, "/v1/auth/email/verify", new { email, code = await factory.Mail.WaitForCode(email), password = AuthApi.Password }));
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

    private sealed class SettableClock(DateTimeOffset start) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = start;
    }

    private sealed record TotpSetup(string Secret, string OtpAuthUri);

    private sealed record TotpEnrollment(AuthApi.Tokens Tokens, IReadOnlyList<string> RecoveryCodes);
}
