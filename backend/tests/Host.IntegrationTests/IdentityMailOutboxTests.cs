using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Identity.Infrastructure;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>
/// Identity mails leave through the module's outbox (ADR-0021, PR 2): an SMTP outage neither fails the request nor
/// loses the mail, and a redelivered message does not mail twice.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class IdentityMailOutboxTests(NirengiApiFactory factory)
{
    private readonly AuthApi _api = new(factory);

    [Fact]
    public async Task Registration_succeeds_while_the_mail_relay_is_down_and_the_code_is_mailed_on_a_later_attempt()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = AuthApi.NewEmail();
        factory.Mail.FailFor(email);
        try
        {
            (await _api.Post("/v1/auth/register", AuthApi.Registration(email))).StatusCode.ShouldBe(HttpStatusCode.Accepted);

            await WaitUntil(() => factory.Mail.FailedSends(email) >= 2, "the outbox did not retry the failed mail", ct);
            factory.Mail.To(email).ShouldBeEmpty();
        }
        finally
        {
            factory.Mail.StopFailing(email);
        }

        var code = await factory.Mail.WaitForCode(email);
        var verified = await _api.Post("/v1/auth/email/verify", new { email, code, password = AuthApi.Password });
        verified.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Forgot_password_succeeds_while_the_mail_relay_is_down_and_the_code_is_mailed_on_a_later_attempt()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = AuthApi.NewEmail();
        await _api.RegisterAndVerify(email);
        var mailsBefore = factory.Mail.To(email).Count;
        factory.Mail.FailFor(email);
        try
        {
            (await _api.Post("/v1/auth/password/forgot", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);

            await WaitUntil(() => factory.Mail.FailedSends(email) >= 2, "the outbox did not retry the failed mail", ct);
            factory.Mail.To(email).Count.ShouldBe(mailsBefore);
        }
        finally
        {
            factory.Mail.StopFailing(email);
        }

        var code = await factory.Mail.WaitForCode(email, mailsBefore);
        var reset = await _api.Post("/v1/auth/password/reset", new { email, code, newPassword = "yeniSifre456" });
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_message_delivered_twice_mails_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var started = DateTimeOffset.UtcNow;
        var email = AuthApi.NewEmail();
        (await _api.Post("/v1/auth/register", AuthApi.Registration(email))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await factory.Mail.WaitForMail(email);
        await factory.WaitForIdentityMailDelivered();

        // Pretend the processor crashed after the handler returned: the message is due again.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var reset = await db.OutboxMessages
                .Where(m => m.Type.Contains("IdentityEmailRequested") && m.OccurredAt >= started)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessedAt, (DateTimeOffset?)null), ct);
            reset.ShouldBe(1);
        }

        await factory.WaitForIdentityMailDelivered();
        factory.Mail.To(email).Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_code_that_was_replaced_before_delivery_is_not_mailed()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = AuthApi.NewEmail();
        using var host = factory.WithWebHostBuilder(b => b.UseSetting("Identity:CodeResendCooldown", "00:00:00"));
        var client = host.CreateClient();
        factory.Mail.FailFor(email);
        try
        {
            (await client.PostAsJsonAsync("/v1/auth/register", AuthApi.Registration(email), AuthApi.Json, ct)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
            await WaitUntil(() => factory.Mail.FailedSends(email) >= 1, "the first mail was not attempted", ct);
            (await client.PostAsJsonAsync("/v1/auth/email/resend", new { email }, AuthApi.Json, ct)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }
        finally
        {
            factory.Mail.StopFailing(email);
        }

        await factory.WaitForIdentityMailDelivered();

        // Only the current code is mailed; the first one would be useless in the mailbox.
        factory.Mail.To(email).Count.ShouldBe(1);
        // Each host derives its own code hash key, so the host that issued the code verifies it.
        var verified = await client.PostAsJsonAsync("/v1/auth/email/verify", new { email, code = factory.Mail.LatestCode(email), password = AuthApi.Password }, AuthApi.Json, ct);
        verified.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task WaitUntil(Func<bool> condition, string failure, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException(failure);
            }

            await Task.Delay(50, ct);
        }
    }
}
