using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Nirengi.Modules.Identity.Application.Abstractions;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Captures identity mails so tests can read the codes a user would receive.</summary>
public sealed partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public Task Send(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> To(string address) =>
        _sent.Where(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase)).ToList();

    public string LatestCode(string address) => SixDigits().Match(To(address)[^1].Subject).Value;

    [GeneratedRegex(@"\d{6}")]
    private static partial Regex SixDigits();
}
