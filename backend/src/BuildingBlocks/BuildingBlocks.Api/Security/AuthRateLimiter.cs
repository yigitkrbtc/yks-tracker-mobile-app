using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Nirengi.BuildingBlocks.Api.Security;

/// <summary>
/// Credential and code endpoints are budgeted per client IP and e-mail address together (security audit G6). The IP alone
/// either locks out a whole school behind one NAT or, set loose, lets one attacker guess at one account all day; the pair
/// gives every account its own budget per address while <see cref="RateLimitPolicies.Auth"/> keeps a looser ceiling per IP.
/// The key is a hash of the address as typed (trimmed, lower case), taken before any lookup, so a limit hit says nothing about
/// whether the account exists.
/// </summary>
public interface IAuthRateLimiter
{
    /// <returns>False when this IP has used up its budget for this e-mail address; <paramref name="retryAfter"/> then says when to retry.</returns>
    bool TryAcquire(HttpContext context, string email, out TimeSpan retryAfter);
}

internal sealed class AuthRateLimiter : IAuthRateLimiter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public AuthRateLimiter(IOptions<AuthRateLimitOptions> options)
    {
        var o = options.Value;
        _limiter = PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = o.PermitLimit, Window = o.Window }));
    }

    public bool TryAcquire(HttpContext context, string email, out TimeSpan retryAfter)
    {
        using var lease = _limiter.AttemptAcquire(Key(ClientIp.Of(context), email));
        if (lease.IsAcquired)
        {
            retryAfter = TimeSpan.Zero;
            return true;
        }

        retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var after) ? after : TimeSpan.FromSeconds(1);
        return false;
    }

    public void Dispose() => _limiter.Dispose();

    /// <summary>Same folding as Identity's Email.Normalize ("İ" to "i", culture-free lower case) so spelling variants share a budget.</summary>
    internal static string Key(string ip, string email)
    {
        var normalized = email.Trim().Replace('İ', 'i').ToLowerInvariant();
        return $"{ip}|{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))}";
    }
}

internal static class ClientIp
{
    public static string Of(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
