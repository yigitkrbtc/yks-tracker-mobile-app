using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Nirengi.BuildingBlocks.Api.Security;

/// <summary>
/// Baseline headers on every response (ADR-0019). API responses are JSON only, so their CSP forbids everything;
/// the admin panel sets its own CSP (see the Host's AdminPanel) before this runs.
/// </summary>
public static class SecurityHeaders
{
    public const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public static WebApplication UseNirengiSecurityHeaders(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            // Only sent over HTTPS (the TLS proxy forwards the scheme); a year, the browser default for preload lists.
            app.UseHsts();
        }

        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers["Cross-Origin-Resource-Policy"] = "same-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
                if (!headers.ContainsKey("Content-Security-Policy"))
                {
                    headers.ContentSecurityPolicy = ApiContentSecurityPolicy;
                }

                return Task.CompletedTask;
            });
            return next(context);
        });
        return app;
    }
}
