using System.Security.Cryptography;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Nirengi.Host;

/// <summary>
/// Serves the admin build (admin/dist, copied to wwwroot/admin by the Dockerfile) at /admin, same origin as the API
/// so no CORS is needed (ADR-0019). index.html gets a fresh CSP nonce per request for Ant Design's runtime styles;
/// scripts load only from 'self'. Absent build (tests, local API without the panel) maps nothing.
/// </summary>
internal static class AdminPanel
{
    public const string PathPrefix = "/admin";

    private const string NoncePlaceholder = "__CSP_NONCE__";

    public static WebApplication MapAdminPanel(this WebApplication app)
    {
        var root = app.Configuration["AdminPanel:Path"] ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot", "admin");
        var index = Path.Combine(root, "index.html");
        if (!File.Exists(index))
        {
            return app;
        }

        var files = new PhysicalFileProvider(Path.GetFullPath(root));

        var types = new FileExtensionContentTypeProvider();

        // One endpoint for files and client routes: static file middleware never runs once routing picked an endpoint.
        app.MapMethods(PathPrefix + "/{**path}", [HttpMethods.Get, HttpMethods.Head], async (HttpContext context, string? path) =>
            {
                var file = string.IsNullOrEmpty(path) ? null : files.GetFileInfo(path);
                if (file is { Exists: true, IsDirectory: false, PhysicalPath: not null } && file.Name != "index.html")
                {
                    context.Response.Headers.ContentSecurityPolicy = Policy(nonce: null);

                    // Vite fingerprints everything under assets/; other files (favicon) revalidate.
                    context.Response.Headers.CacheControl = path!.StartsWith("assets/", StringComparison.Ordinal)
                        ? "public, max-age=31536000, immutable"
                        : "public, max-age=3600";
                    return Results.File(file.PhysicalPath, types.TryGetContentType(file.Name, out var type) ? type : "application/octet-stream");
                }

                if (path is not null && (path.StartsWith("assets/", StringComparison.Ordinal) || Path.HasExtension(path)))
                {
                    return Results.NotFound(); // a missing file is not a client route
                }

                var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
                context.Response.Headers.ContentSecurityPolicy = Policy(nonce);
                context.Response.Headers.CacheControl = "no-store";
                // Read per request (under a kilobyte): a rebuilt panel is picked up without restarting the API.
                var html = await File.ReadAllTextAsync(index, context.RequestAborted);
                return Results.Content(html.Replace(NoncePlaceholder, nonce, StringComparison.Ordinal), "text/html; charset=utf-8");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
        return app;
    }

    /// <summary>style-src-attr allows the style="" attributes React sets; style elements need the nonce.</summary>
    internal static string Policy(string? nonce) =>
        "default-src 'none'; script-src 'self'; "
        + (nonce is null ? "style-src 'self'; " : $"style-src 'self' 'nonce-{nonce}' 'report-sample'; ")
        + "style-src-attr 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; manifest-src 'self'; "
        + "frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
}
