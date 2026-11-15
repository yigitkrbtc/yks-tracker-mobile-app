using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Nirengi.BuildingBlocks.Api.Security;

/// <summary>
/// Request size caps (security audit G7). The default is <see cref="MaxBodyBytes"/>: nearly every write the API takes is a
/// small JSON document, and the biggest of them is a mock exam with the maximum 300 topic marks, about 57 KB with every
/// field filled (measured in <c>ApiBoundaryTests.The_largest_legitimate_body_fits_the_limit_with_room_to_spare</c>). There
/// is no upload or bulk endpoint: sync is a GET and records are written one by one.
/// <para>
/// The one exception is the admin curriculum draft, which sends its whole operation list on every save (up to
/// <c>DraftApplier.MaxOperations</c> = 500). An endpoint raises its own cap with <see cref="AllowBodyUpTo{TBuilder}"/>,
/// which adds <see cref="IRequestSizeLimitMetadata"/> to the endpoint: Kestrel's global limit is only the default, and the
/// endpoint middleware applies the metadata to the request before the body is read (chunked bodies included). The
/// <c>Content-Length</c> middleware reads the same metadata, so a declared oversize body is answered with 413 without
/// reading anything, against the cap of the endpoint the request is for.
/// </para>
/// </summary>
public static class RequestLimits
{
    public const int MaxBodyBytes = 64 * 1024;

    /// <summary>
    /// Admin curriculum draft save: 1 MB, from 500 operations (<c>DraftApplier.MaxOperations</c>) times about 2 KB each. The
    /// biggest topic of the seed is about 430 bytes as an operation, so a full draft is around 250 KB and an operation may
    /// be four times the seed's biggest. Operations filled to every field limit (up to roughly 13 KB each) would not fit
    /// 500 times; no editor produces that, and the endpoint is admin-only, authenticated and rate limited before the
    /// body is read.
    /// </summary>
    public const int MaxDraftBodyBytes = 1024 * 1024;

    /// <summary>Raises the body cap of the endpoint (or group) to <paramref name="bytes"/>; every other endpoint keeps <see cref="MaxBodyBytes"/>.</summary>
    public static TBuilder AllowBodyUpTo<TBuilder>(this TBuilder builder, int bytes)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new BodyLimit(bytes));

    internal static void Configure(IServiceCollection services) =>
        services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = MaxBodyBytes);

    internal static void Use(WebApplication app) =>
        app.Use((context, next) =>
        {
            var limit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize ?? MaxBodyBytes;
            if (context.Request.ContentLength > limit)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return Task.CompletedTask;
            }

            return next(context);
        });

    private sealed record BodyLimit(long Bytes) : IRequestSizeLimitMetadata
    {
        public long? MaxRequestBodySize => Bytes;
    }
}
