using System.Net;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class AuthFlowTests(NirengiApiFactory factory)
{
    private readonly AuthApi _api = new(factory);

    [Fact]
    public async Task Register_verify_then_read_profile()
    {
        var email = AuthApi.NewEmail();

        var tokens = await _api.RegisterAndVerify(email);

        var me = await _api.GetMe(tokens.AccessToken);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await AuthApi.Read<AuthApi.Profile>(me);
        profile.Email.ShouldBe(email);
        profile.FirstName.ShouldBe("Ada");
        profile.Role.ShouldBe("Student");
        profile.CanGiveMarketingConsent.ShouldBeTrue();
        tokens.RefreshTokenExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(29));
        (tokens.AccessTokenExpiresAt - DateTimeOffset.UtcNow).ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task Profile_requires_a_token()
    {
        var response = await _api.Client.GetAsync("/v1/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Under_13_cannot_register()
    {
        var year = DateTimeOffset.UtcNow.Year;

        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/register", AuthApi.Registration(AuthApi.NewEmail(), birthYear: year - 13)),
            HttpStatusCode.BadRequest,
            "identity.underage");
        (await _api.Post("/v1/auth/register", AuthApi.Registration(AuthApi.NewEmail(), birthYear: year - 14)))
            .StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Minor_cannot_give_marketing_consent()
    {
        var minorYear = DateTimeOffset.UtcNow.Year - 18;

        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/register", AuthApi.Registration(AuthApi.NewEmail(), birthYear: minorYear, marketing: true)),
            HttpStatusCode.BadRequest,
            "identity.marketing_consent_not_allowed");

        var email = AuthApi.NewEmail();
        (await _api.Post("/v1/auth/register", AuthApi.Registration(email, birthYear: minorYear))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var verified = await _api.Post("/v1/auth/email/verify", new { email, code = factory.Mail.LatestCode(email) });
        var profile = await AuthApi.Read<AuthApi.Profile>(await _api.GetMe((await AuthApi.Read<AuthApi.SignIn>(verified)).Tokens!.AccessToken));
        profile.CanGiveMarketingConsent.ShouldBeFalse();
    }

    [Fact]
    public async Task Outdated_terms_are_refused()
    {
        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/register", AuthApi.Registration(AuthApi.NewEmail(), terms: "0")),
            HttpStatusCode.Conflict,
            "identity.outdated_terms");
    }

    [Fact]
    public async Task Weak_password_is_a_validation_problem()
    {
        var response = await _api.Post("/v1/auth/register", new
        {
            firstName = "Ada", lastName = "Yılmaz", email = AuthApi.NewEmail(), password = "kisa", birthYear = 2000,
            termsVersion = "1", privacyNoticeVersion = "1", marketingConsent = false,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Registering_a_taken_address_reveals_nothing_and_keeps_the_account()
    {
        var email = AuthApi.NewEmail();
        await _api.RegisterAndVerify(email);

        (await _api.Post("/v1/auth/register", AuthApi.Registration(email))).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        factory.Mail.To(email)[^1].Subject.ShouldContain("zaten var");
        (await _api.Post("/v1/auth/login", new { email, password = AuthApi.Password })).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Turkish_capital_dotted_i_is_the_same_mailbox()
    {
        var local = Guid.NewGuid().ToString("N");
        await _api.RegisterAndVerify($"İ{local}@example.com");

        var login = await _api.Post("/v1/auth/login", new { email = $"i{local}@EXAMPLE.com", password = AuthApi.Password });

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unverified_account_cannot_log_in_and_wrong_code_fails()
    {
        var email = AuthApi.NewEmail();
        await _api.Post("/v1/auth/register", AuthApi.Registration(email));

        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/login", new { email, password = AuthApi.Password }),
            HttpStatusCode.Forbidden,
            "identity.email_not_verified");

        var wrong = factory.Mail.LatestCode(email) == "000000" ? "111111" : "000000";
        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/email/verify", new { email, code = wrong }),
            HttpStatusCode.BadRequest,
            "identity.invalid_code");
    }

    [Fact]
    public async Task Resend_respects_the_cooldown()
    {
        var email = AuthApi.NewEmail();
        await _api.Post("/v1/auth/register", AuthApi.Registration(email));

        (await _api.Post("/v1/auth/email/resend", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        factory.Mail.To(email).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_rotates_and_reuse_kills_the_family()
    {
        var first = await _api.RegisterAndVerify(AuthApi.NewEmail());

        var rotated = await _api.Post("/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = await AuthApi.Read<AuthApi.Tokens>(rotated);
        second.RefreshToken.ShouldNotBe(first.RefreshToken);
        second.RefreshTokenExpiresAt.ShouldBe(first.RefreshTokenExpiresAt, TimeSpan.FromMilliseconds(1));

        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/refresh", new { refreshToken = first.RefreshToken }),
            HttpStatusCode.Unauthorized,
            "identity.invalid_refresh_token");
        (await _api.Post("/v1/auth/refresh", new { refreshToken = second.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Concurrent_refreshes_of_one_token_let_at_most_one_through()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => _api.Post("/v1/auth/refresh", new { refreshToken = tokens.RefreshToken })));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Unauthorized);
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task Concurrent_sign_ups_for_one_address_all_get_202()
    {
        var email = AuthApi.NewEmail();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => _api.Post("/v1/auth/register", AuthApi.Registration(email))));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task An_address_the_mailer_cannot_parse_still_gets_202()
    {
        var response = await _api.Post("/v1/auth/register", AuthApi.Registration($"a\"{Guid.NewGuid():N}@example.com"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        (await _api.Post("/v1/auth/logout", new { refreshToken = tokens.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _api.Post("/v1/auth/refresh", new { refreshToken = tokens.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ten_wrong_passwords_lock_the_account()
    {
        var email = AuthApi.NewEmail();
        await _api.RegisterAndVerify(email);

        for (var i = 0; i < 10; i++)
        {
            await AuthApi.ShouldBeProblem(
                await _api.Post("/v1/auth/login", new { email, password = "yanlisSifre99" }),
                HttpStatusCode.Unauthorized,
                "identity.invalid_credentials");
        }

        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/login", new { email, password = AuthApi.Password }),
            HttpStatusCode.Unauthorized,
            "identity.invalid_credentials");
    }

    [Fact]
    public async Task Unknown_address_gets_the_same_answer_as_a_wrong_password()
    {
        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/login", new { email = AuthApi.NewEmail(), password = AuthApi.Password }),
            HttpStatusCode.Unauthorized,
            "identity.invalid_credentials");
    }
}
