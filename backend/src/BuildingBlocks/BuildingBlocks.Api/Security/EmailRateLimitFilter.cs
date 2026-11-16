using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace Nirengi.BuildingBlocks.Api.Security;

/// <summary>
/// Endpoint filter for credential and code endpoints: charges the request to the bound command's <c>Email</c> (any argument
/// with a public string property of that name) in <see cref="IAuthRateLimiter"/>, after <see cref="RateLimitPolicies.Auth"/> has
/// charged the IP. It runs after model binding, which is why it is a filter and not a rate limit policy: a policy sees the
/// request before the body is read. A body without an address (or one that fails to bind) is left to the IP limit.
/// </summary>
public sealed class EmailRateLimitFilter(IAuthRateLimiter limiter) : IEndpointFilter
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> EmailProperties = new();

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var email = context.Arguments.Select(EmailOf).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
        if (email is not null && !limiter.TryAcquire(context.HttpContext, email, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return ValueTask.FromResult<object?>(Microsoft.AspNetCore.Http.Results.StatusCode(StatusCodes.Status429TooManyRequests));
        }

        return next(context);
    }

    private static string? EmailOf(object? argument)
    {
        if (argument is null)
        {
            return null;
        }

        var property = EmailProperties.GetOrAdd(
            argument.GetType(),
            type => type.GetProperty("Email", BindingFlags.Public | BindingFlags.Instance) is { PropertyType: var t } p && t == typeof(string) ? p : null);
        return property?.GetValue(argument) as string;
    }
}
