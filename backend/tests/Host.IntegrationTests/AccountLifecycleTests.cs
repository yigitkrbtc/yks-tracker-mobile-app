using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Identity.Application.Features.Deletion;
using Nirengi.Modules.Identity.Domain.Tokens;
using Nirengi.Modules.Identity.Infrastructure;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class AccountLifecycleTests(NirengiApiFactory factory)
{
    private const string NewPassword = "yeniSifre456";
    private readonly AuthApi _api = new(factory);

    [Fact]
    public async Task Password_reset_replaces_the_password_and_signs_out_everywhere()
    {
        var email = AuthApi.NewEmail();
        var tokens = await _api.RegisterAndVerify(email);

        var mailsBefore = factory.Mail.To(email).Count;
        (await _api.Post("/v1/auth/password/forgot", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var code = await factory.Mail.WaitForCode(email, mailsBefore);
        (await _api.Post("/v1/auth/password/reset", new { email, code, newPassword = NewPassword })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _api.Post("/v1/auth/refresh", new { refreshToken = tokens.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _api.Post("/v1/auth/login", new { email, password = AuthApi.Password })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _api.Post("/v1/auth/login", new { email, password = NewPassword })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.Post("/v1/auth/password/reset", new { email, code, newPassword = "baskaSifre789" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Forgot_password_for_unknown_address_looks_the_same()
    {
        var email = AuthApi.NewEmail();

        (await _api.Post("/v1/auth/password/forgot", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        factory.Mail.To(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task Deletion_signs_out_blocks_login_and_can_be_restored()
    {
        var email = AuthApi.NewEmail();
        var tokens = await _api.RegisterAndVerify(email);

        await AuthApi.ShouldBeProblem(await RequestDeletion(tokens.AccessToken, "yanlisSifre99"), HttpStatusCode.BadRequest, "identity.invalid_password");

        var scheduled = await RequestDeletion(tokens.AccessToken, AuthApi.Password);
        scheduled.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await AuthApi.Read<DeletionScheduled>(scheduled)).PurgeAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(29));

        (await _api.Post("/v1/auth/refresh", new { refreshToken = tokens.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await AuthApi.ShouldBeProblem(
            await _api.Post("/v1/auth/login", new { email, password = AuthApi.Password }),
            HttpStatusCode.Forbidden,
            "identity.deletion_pending");

        var restored = await _api.Post("/v1/auth/account/restore", new { email, password = AuthApi.Password });
        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AuthApi.Read<AuthApi.SignIn>(restored)).Tokens.ShouldNotBeNull();
        (await _api.Post("/v1/auth/login", new { email, password = AuthApi.Password })).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deletion_password_guesses_count_toward_lockout()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        for (var i = 0; i < 10; i++)
        {
            await AuthApi.ShouldBeProblem(await RequestDeletion(tokens.AccessToken, "yanlisSifre99"), HttpStatusCode.BadRequest, "identity.invalid_password");
        }

        await AuthApi.ShouldBeProblem(await RequestDeletion(tokens.AccessToken, AuthApi.Password), HttpStatusCode.BadRequest, "identity.invalid_password");
    }

    [Fact]
    public async Task Access_token_issued_before_deletion_no_longer_reads_the_profile()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        (await RequestDeletion(tokens.AccessToken, AuthApi.Password)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        (await _api.GetMe(tokens.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Purge_removes_expired_accounts_and_announces_them()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = AuthApi.NewEmail();
        var tokens = await _api.RegisterAndVerify(email);
        (await RequestDeletion(tokens.AccessToken, AuthApi.Password)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await db.Users.AsNoTracking().SingleAsync(u => u.Email == email, ct)).Id;
        await db.Database.ExecuteSqlAsync(
            $"UPDATE identity.users SET deletion_requested_at = now() - interval '31 days' WHERE id = {userId}", ct);

        var purged = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new PurgeDeletedAccountsCommand(), ct);

        purged.Value.ShouldBeGreaterThanOrEqualTo(1);
        (await db.Users.AnyAsync(u => u.Id == userId, ct)).ShouldBeFalse();
        (await db.RefreshTokens.AnyAsync(t => t.UserId == userId, ct)).ShouldBeFalse();
        (await db.ConsentRecords.AnyAsync(c => c.UserId == userId, ct)).ShouldBeFalse();
        var announced = await db.OutboxMessages
            .Where(m => m.Type == "identity.user-deleted")
            .Select(m => m.Payload)
            .ToListAsync(ct);
        announced.ShouldContain(p => p.Contains(userId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Purge_removes_sign_ups_left_unverified_for_a_week()
    {
        var ct = TestContext.Current.CancellationToken;
        var stale = AuthApi.NewEmail();
        var fresh = AuthApi.NewEmail();
        await _api.Post("/v1/auth/register", AuthApi.Registration(stale));
        await _api.Post("/v1/auth/register", AuthApi.Registration(fresh));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE identity.users SET created_at = now() - interval '8 days' WHERE email = {stale}", ct);

        (await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new PurgeDeletedAccountsCommand(), ct)).IsSuccess.ShouldBeTrue();

        (await db.Users.AnyAsync(u => u.Email == stale, ct)).ShouldBeFalse();
        (await db.Users.AnyAsync(u => u.Email == fresh, ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Purge_deletes_refresh_tokens_expired_over_a_week_ago_and_nothing_else()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = AuthApi.NewEmail();
        await _api.RegisterAndVerify(email);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await db.Users.AsNoTracking().SingleAsync(u => u.Email == email, ct)).Id;
        var now = DateTimeOffset.UtcNow;

        // Issue(.., now, lifetime): a past `now` plus a short lifetime gives a token that expired `expiredAgo` ago.
        RefreshToken Expired(TimeSpan expiredAgo, bool revoked)
        {
            var token = RefreshToken.Issue(userId, Guid.NewGuid().ToString("N"), now - expiredAgo - TimeSpan.FromHours(1), TimeSpan.FromHours(1), issuedWithMfa: false);
            if (revoked)
            {
                token.Revoke(now, RevokeReason.Logout);
            }

            return token;
        }

        var longExpired = Expired(TimeSpan.FromDays(8), revoked: false);
        var longExpiredRevoked = Expired(TimeSpan.FromDays(8), revoked: true);
        var recentlyExpired = Expired(TimeSpan.FromDays(6), revoked: false);
        var revokedNotExpired = RefreshToken.Issue(userId, Guid.NewGuid().ToString("N"), now, TimeSpan.FromDays(30), issuedWithMfa: false);
        revokedNotExpired.Revoke(now, RevokeReason.Rotated);
        db.RefreshTokens.AddRange(longExpired, longExpiredRevoked, recentlyExpired, revokedNotExpired);
        await db.SaveChangesAsync(ct);

        (await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new PurgeDeletedAccountsCommand(), ct)).IsSuccess.ShouldBeTrue();

        var remaining = await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId).Select(t => t.Id).ToListAsync(ct);
        remaining.ShouldNotContain(longExpired.Id);
        remaining.ShouldNotContain(longExpiredRevoked.Id);
        remaining.ShouldContain(recentlyExpired.Id);
        remaining.ShouldContain(revokedNotExpired.Id);
    }

    private Task<HttpResponseMessage> RequestDeletion(string accessToken, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/me/deletion") { Content = JsonContent.Create(new { password }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _api.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
