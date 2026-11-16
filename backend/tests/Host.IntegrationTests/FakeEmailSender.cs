using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Nirengi.Modules.Identity.Application.Abstractions;

namespace Nirengi.Host.IntegrationTests;

/// <summary>
/// Captures identity mails so tests can read the codes a user would receive. Mails leave through the Identity outbox,
/// so they arrive a moment after the request: read them with the <c>Wait*</c> methods.
/// </summary>
public sealed partial class FakeEmailSender : IEmailSender
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private readonly ConcurrentQueue<EmailMessage> _sent = new();
    private readonly ConcurrentDictionary<string, int> _failing = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _failures = new(StringComparer.OrdinalIgnoreCase);

    public Task Send(EmailMessage message, CancellationToken cancellationToken)
    {
        if (_failing.ContainsKey(message.To))
        {
            _failures.AddOrUpdate(message.To, 1, (_, n) => n + 1);
            throw new InvalidOperationException("simulated SMTP outage");
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Every send to this address throws until <see cref="StopFailing"/>.</summary>
    public void FailFor(string address) => _failing[address] = 0;

    public void StopFailing(string address) => _failing.TryRemove(address, out _);

    /// <summary>Number of sends to this address that threw.</summary>
    public int FailedSends(string address) => _failures.GetValueOrDefault(address);

    public IReadOnlyList<EmailMessage> To(string address) =>
        _sent.Where(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Waits until more than <paramref name="after"/> mails went to the address; returns the newest.</summary>
    public async Task<EmailMessage> WaitForMail(string address, int after = 0, TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? DefaultTimeout);
        while (To(address).Count <= after)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"No mail number {after + 1} reached the address within the timeout.");
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return To(address)[^1];
    }

    public async Task<string> WaitForCode(string address, int after = 0) =>
        EightDigits().Match((await WaitForMail(address, after)).Subject).Value;

    /// <summary>Newest code mailed to the address; only valid once the outbox has delivered everything.</summary>
    public string LatestCode(string address) => EightDigits().Match(To(address)[^1].Subject).Value;

    [GeneratedRegex(@"\d{8}")]
    private static partial Regex EightDigits();
}
