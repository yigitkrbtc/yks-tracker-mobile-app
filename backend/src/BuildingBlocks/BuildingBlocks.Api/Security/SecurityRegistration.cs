using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Security;

namespace Nirengi.BuildingBlocks.Api.Security;

public static class Policies
{
    public const string Admin = "admin";
}

public static class RateLimitPolicies
{
    /// <summary>
    /// Credential and code endpoints (password guessing): a ceiling per client IP. Endpoints that take an e-mail address also
    /// carry <see cref="EmailRateLimitFilter"/>, which budgets each IP and address pair (G6).
    /// </summary>
    public const string Auth = "auth";

    /// <summary>Token refresh and logout: routine traffic, so its own looser budget, partitioned by client IP.</summary>
    public const string Session = "session";

    /// <summary>Personal data export: heavy reads across every module, partitioned by user.</summary>
    public const string Export = "export";

    /// <summary>Admin panel endpoints, partitioned by user.</summary>
    public const string Admin = "admin";

    /// <summary>
    /// General budget for the signed-in student's data endpoints, partitioned by user id (G7). Applied to the whole <c>/v1</c>
    /// group; endpoints with a policy of their own keep it, and anonymous requests are not limited by it (public, cacheable reads;
    /// credential endpoints have <see cref="Auth"/>).
    /// </summary>
    public const string Api = "api";
}

public sealed class ApiRateLimitOptions
{
    /// <summary>
    /// A busy day of use is a few dozen calls: sync pages, a record write per saved test, the panel, the plan. 300 a minute is
    /// far above that and far below scraping or flooding the database.
    /// </summary>
    public int PermitLimit { get; set; } = 300;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

public sealed class AdminRateLimitOptions
{
    public int PermitLimit { get; set; } = 120;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

public sealed class ExportRateLimitOptions
{
    public int PermitLimit { get; set; } = 3;

    public TimeSpan Window { get; set; } = TimeSpan.FromHours(1);
}

public sealed class AuthRateLimitOptions
{
    /// <summary>Attempts per client IP and e-mail address together, per window.</summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>Attempts per client IP over all addresses, per window: loose enough for a classroom behind one NAT.</summary>
    public int PerIpPermitLimit { get; set; } = 60;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

public sealed class SessionRateLimitOptions
{
    public int PermitLimit { get; set; } = 60;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

public static class SecurityRegistration
{
    /// <summary>JWT bearer validation, authorization policies, the current user and auth rate limiting.</summary>
    public static IServiceCollection AddNirengiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(o => SigningKeyLength(o.SigningKey) >= 32, "Jwt:SigningKey must be base64 of at least 32 bytes.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(jwt.Value.SigningKeyBytes()),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = NirengiClaims.Subject,
                    RoleClaimType = NirengiClaims.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, p => p.RequireAuthenticatedUser().RequireRole(NirengiRoles.Admin));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // Rate limits partition by client IP, so behind a reverse proxy its X-Forwarded-For must be honoured.
        // Only listed proxies and networks (plus loopback) are trusted; anyone else could spoof the header. Production
        // refuses to start with neither listed (ProductionSafetyValidator).
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in ForwardedHeadersSettings.Proxies(configuration))
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in ForwardedHeadersSettings.Networks(configuration))
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        services.AddSingleton<IValidateOptions<ProductionSafetyOptions>, ProductionSafetyValidator>();
        services.AddOptions<ProductionSafetyOptions>().ValidateOnStart();
        RequestLimits.Configure(services);

        var auth = configuration.GetSection("RateLimiting:Auth").Get<AuthRateLimitOptions>() ?? new AuthRateLimitOptions();
        var api = configuration.GetSection("RateLimiting:Api").Get<ApiRateLimitOptions>() ?? new ApiRateLimitOptions();
        services.AddSingleton<IOptions<AuthRateLimitOptions>>(Options.Create(auth));
        services.AddSingleton<IAuthRateLimiter, AuthRateLimiter>();
        var session = configuration.GetSection("RateLimiting:Session").Get<SessionRateLimitOptions>() ?? new SessionRateLimitOptions();
        var export = configuration.GetSection("RateLimiting:Export").Get<ExportRateLimitOptions>() ?? new ExportRateLimitOptions();
        var admin = configuration.GetSection("RateLimiting:Admin").Get<AdminRateLimitOptions>() ?? new AdminRateLimitOptions();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = auth.PerIpPermitLimit, Window = auth.Window }));
            options.AddPolicy(RateLimitPolicies.Api, context => context.User.FindFirst(NirengiClaims.Subject)?.Value is { } userId
                ? RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions { PermitLimit = api.PermitLimit, Window = api.Window })
                : RateLimitPartition.GetNoLimiter("anonymous"));
            options.AddPolicy(RateLimitPolicies.Session, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = session.PermitLimit, Window = session.Window }));
            options.AddPolicy(RateLimitPolicies.Export, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(NirengiClaims.Subject)?.Value ?? ClientIp(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = export.PermitLimit, Window = export.Window }));
            options.AddPolicy(RateLimitPolicies.Admin, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(NirengiClaims.Subject)?.Value ?? ClientIp(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = admin.PermitLimit, Window = admin.Window }));
        });

        return services;
    }

    /// <summary>Call first in the pipeline, so logging and rate limiting see the real client IP.</summary>
    public static WebApplication UseNirengiForwardedHeaders(this WebApplication app)
    {
        app.UseForwardedHeaders();
        return app;
    }

    public static WebApplication UseNirengiSecurity(this WebApplication app)
    {
        RequestLimits.Use(app);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        return app;
    }

    private static string ClientIp(HttpContext context) => Security.ClientIp.Of(context);

    private static int SigningKeyLength(string key)
    {
        var buffer = new byte[key.Length];
        return Convert.TryFromBase64String(key, buffer, out var written) ? written : 0;
    }
}
