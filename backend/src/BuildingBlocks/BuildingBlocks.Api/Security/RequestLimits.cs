using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Nirengi.BuildingBlocks.Api.Security;

/// <summary>
/// Request size cap (security audit G7). Every write the API takes is a small JSON document; the biggest legitimate one is a
/// mock exam with the maximum 300 topic marks, about 57 KB with every field filled (measured in
/// <c>ApiBoundaryTests.The_largest_legitimate_body_fits_the_limit_with_room_to_spare</c>). There is no upload or bulk endpoint:
/// sync is a GET and records are written one by one. Kestrel enforces the cap while reading (chunked bodies included); the
/// middleware answers a declared oversize <c>Content-Length</c> without reading anything.
/// </summary>
public static class RequestLimits
{
    public const int MaxBodyBytes = 64 * 1024;

    internal static void Configure(IServiceCollection services) =>
        services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = MaxBodyBytes);

    internal static void Use(WebApplication app) =>
        app.Use((context, next) =>
        {
            if (context.Request.ContentLength > MaxBodyBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return Task.CompletedTask;
            }

            return next(context);
        });
}
