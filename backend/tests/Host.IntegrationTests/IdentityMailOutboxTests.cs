using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Nirengi.Modules.Identity.Application.Abstractions;
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
            var protector = scope.ServiceProvider.GetRequiredService<IEmailPayloadProtector>();
            // The database is shared with other test classes, so pick this test's row by opening the payloads.
            var candidates = await db.OutboxMessages
                .Where(m => m.Type.Contains("IdentityEmailRequested") && m.OccurredAt >= started)
                .ToListAsync(ct);
            var mine = candidates.Where(m =>
            {
                var e = JsonSerializer.Deserialize<IdentityEmailRequested>(m.Payload)!;
                return protector.Unprotect(e.ProtectedPayload, IdentityEmailRequested.AssociatedData(e.Id, e.ValidUntil))
                    .Contains(email, StringComparison.OrdinalIgnoreCase);
            }).ToList();
            mine.Count.ShouldBe(1);
            mine[0].ProcessedAt = null;
            await db.SaveChangesAsync(ct);
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
        var verified = await client.PostAsJsonAsync("/v1/auth/email/verify", new { email, code = factory.Mail.LatestCode(email), password = AuthApi.Password }, AuthApi.Json, ct);
        verified.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Outbox_rows_hold_nothing_readable_and_the_key_ring_in_the_database_does_not_open_them()
    {
        var ct = TestContext.Current.CancellationToken;
        var started = DateTimeOffset.UtcNow;
        var email = AuthApi.NewEmail();
        const string firstName = "Nurtenzehra";
        factory.Mail.FailFor(email);
        try
        {
            var registration = new { firstName, lastName = "Karaoglu", email, password = AuthApi.Password, birthYear = 2000, termsVersion = "1", privacyNoticeVersion = "1", marketingConsent = false };
            (await _api.Post("/v1/auth/register", registration)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
            await WaitUntil(() => factory.Mail.FailedSends(email) >= 1, "the mail was not attempted", ct);

            // While the relay is down the row carries a last_error; it must not echo the mail either.
            var failing = await MailRows(started, ct);
            failing.ShouldNotBeEmpty();
            failing.ShouldContain(r => r.LastError != null);
            AssertNothingReadable(failing, email, firstName);
        }
        finally
        {
            factory.Mail.StopFailing(email);
        }

        var code = await factory.Mail.WaitForCode(email);
        await _api.Post("/v1/auth/email/verify", new { email, code, password = AuthApi.Password });
        var mailsBefore = factory.Mail.To(email).Count;
        (await _api.Post("/v1/auth/password/forgot", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var resetCode = await factory.Mail.WaitForCode(email, mailsBefore);
        await factory.WaitForIdentityMailDelivered();

        var rows = await MailRows(started, ct);
        rows.Count.ShouldBeGreaterThanOrEqualTo(2);
        AssertNothingReadable(rows, email, firstName, code, resetCode);

        // Someone who can read the whole database has the key ring, but not the HashKey that protects the payload.
        await using var scope = factory.Services.CreateAsyncScope();
        var xml = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().DataProtectionKeys.Select(k => k.Xml).ToListAsync(ct);
        var keyDirectory = Directory.CreateTempSubdirectory("nirengi-keyring-");
        try
        {
            for (var i = 0; i < xml.Count; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(keyDirectory.FullName, $"key-{i}.xml"), xml[i], ct);
            }

            var attacker = DataProtectionProvider.Create(keyDirectory, b => b.SetApplicationName("Nirengi")).CreateProtector("Nirengi.Identity.EmailPayload");
            foreach (var row in rows)
            {
                var payload = JsonSerializer.Deserialize<IdentityEmailRequested>(row.Payload)!.ProtectedPayload;
                Should.Throw<CryptographicException>(() => attacker.Unprotect(payload));
            }
        }
        finally
        {
            keyDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_mail_past_its_validity_is_dropped_unsent()
    {
        var ct = TestContext.Current.CancellationToken;
        var live = AuthApi.NewEmail();
        var expired = AuthApi.NewEmail();
        var now = DateTimeOffset.UtcNow;
        var liveId = await EnqueueMail(live, validUntil: now + TimeSpan.FromHours(1), ct);
        var expiredId = await EnqueueMail(expired, validUntil: now - TimeSpan.FromMinutes(1), ct);

        await WaitUntilProcessed(liveId, ct);
        await WaitUntilProcessed(expiredId, ct);

        // The live control mail proves the pipeline delivered; the expired one was consumed without sending.
        factory.Mail.To(live).Count.ShouldBe(1);
        factory.Mail.To(expired).ShouldBeEmpty();
        (await Find(expiredId, ct)).Attempts.ShouldBe(0);
    }

    [Fact]
    public async Task A_payload_that_cannot_be_read_is_dropped_without_retrying_forever()
    {
        var ct = TestContext.Current.CancellationToken;
        var live = AuthApi.NewEmail();
        var tampered = AuthApi.NewEmail();
        var now = DateTimeOffset.UtcNow;
        var liveId = await EnqueueMail(live, now + TimeSpan.FromHours(1), ct);

        var validUntil = now + TimeSpan.FromHours(1);
        var tamperedEventId = Guid.CreateVersion7();
        var raw = Convert.FromBase64String(Protect(tampered, tamperedEventId, validUntil));
        raw[^1] ^= 0x01;
        var tamperedId = await EnqueueRaw(Convert.ToBase64String(raw), validUntil, ct, tamperedEventId);
        var garbageId = await EnqueueRaw("bozuk-yuk", validUntil, ct);

        // A genuine payload copied into a row with another id (or deadline) is not honoured either.
        var copied = AuthApi.NewEmail();
        var copiedFrom = Guid.CreateVersion7();
        var copiedPayload = Protect(copied, copiedFrom, validUntil);
        var copiedId = await EnqueueRaw(copiedPayload, validUntil, ct);
        var extendedId = await EnqueueRaw(copiedPayload, validUntil + TimeSpan.FromDays(30), ct);

        foreach (var id in new[] { liveId, tamperedId, garbageId, copiedId, extendedId })
        {
            await WaitUntilProcessed(id, ct);
        }

        factory.Mail.To(live).Count.ShouldBe(1);
        factory.Mail.To(tampered).ShouldBeEmpty();
        factory.Mail.To(copied).ShouldBeEmpty();
        foreach (var id in new[] { tamperedId, garbageId, copiedId, extendedId })
        {
            (await Find(id, ct)).Attempts.ShouldBe(0);
        }
    }

    private string Protect(string to, Guid id, DateTimeOffset validUntil) =>
        factory.Services.GetRequiredService<IEmailPayloadProtector>().Protect(
            JsonSerializer.Serialize(new IdentityEmailPayload(new EmailMessage(to, "Konu", "Gövde"), null)),
            IdentityEmailRequested.AssociatedData(id, validUntil));

    private Task<Guid> EnqueueMail(string to, DateTimeOffset validUntil, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        return EnqueueRaw(Protect(to, id, validUntil), validUntil, ct, id);
    }

    private async Task<Guid> EnqueueRaw(string protectedPayload, DateTimeOffset validUntil, CancellationToken ct, Guid? id = null)
    {
        var now = DateTimeOffset.UtcNow;
        var integrationEvent = new IdentityEmailRequested(id ?? Guid.CreateVersion7(now), now, validUntil, protectedPayload);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Enqueue(integrationEvent);
        await db.SaveChangesAsync(ct);
        return integrationEvent.Id;
    }

    private async Task<OutboxMessage> Find(Guid id, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id, ct);
    }

    private async Task WaitUntilProcessed(Guid id, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
        while ((await Find(id, ct)).ProcessedAt is null)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Outbox message {id} was not processed.");
            }

            await Task.Delay(50, ct);
        }
    }

    private async Task<List<OutboxMessage>> MailRows(DateTimeOffset since, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().OutboxMessages.AsNoTracking()
            .Where(m => m.Type.Contains("IdentityEmailRequested") && m.OccurredAt >= since)
            .ToListAsync(ct);
    }

    private static void AssertNothingReadable(List<OutboxMessage> rows, params string[] secrets)
    {
        foreach (var row in rows)
        {
            foreach (var secret in secrets)
            {
                row.Payload.ShouldNotContain(secret);
                row.Type.ShouldNotContain(secret);
                (row.LastError ?? string.Empty).ShouldNotContain(secret);
            }
        }
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
