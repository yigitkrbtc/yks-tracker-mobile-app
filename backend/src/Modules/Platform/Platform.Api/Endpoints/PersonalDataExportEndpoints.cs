using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Security;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Export;

namespace Nirengi.Modules.Platform.Api.Endpoints;

/// <summary>
/// KVKK data portability (ADR-0018): one ZIP of CSVs from every module's <see cref="IPersonalDataExporter"/>.
/// Platform knows no module; each exporter reads only its own schema.
/// </summary>
internal static class PersonalDataExportEndpoints
{
    internal const string ReadMeName = "BENIOKU.txt";

    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static void MapPersonalDataExport(this IEndpointRouteBuilder v1) =>
        v1.MapGet("/me/export", async (ICurrentUser user, IEnumerable<IPersonalDataExporter> exporters, IClock clock, CancellationToken ct) =>
            {
                var now = clock.UtcNow;
                var files = new List<ExportFile>();
                foreach (var exporter in exporters.OrderBy(e => e.Order))
                {
                    files.AddRange((await exporter.Export(user.UserId, ct)).OrderBy(f => f.Name, StringComparer.Ordinal));
                }

                var zip = Build(files, now);
                var day = now.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                return Results.File(zip, "application/zip", $"nirengi-verilerim-{day}.zip");
            })
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("ExportPersonalData")
            .WithSummary("Download everything the student entered as a ZIP of semicolon-separated UTF-8 CSVs. 3 per hour.")
            .Produces<byte[]>(StatusCodes.Status200OK, "application/zip")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    internal static byte[] Build(IReadOnlyList<ExportFile> files, DateTimeOffset now)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, ReadMeName, ReadMe(files, now), now);
            foreach (var file in files)
            {
                Write(archive, file.Name, file.Content, now);
            }
        }

        return buffer.ToArray();
    }

    private static void Write(ZipArchive archive, string name, string content, DateTimeOffset now)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = now;
        using var stream = entry.Open();
        var bytes = Utf8WithBom.GetBytes(content);
        stream.Write(Utf8WithBom.GetPreamble());
        stream.Write(bytes);
    }

    private static string ReadMe(IReadOnlyList<ExportFile> files, DateTimeOffset now)
    {
        var text = new StringBuilder()
            .Append("Nirengi kişisel veri dışa aktarımı\r\n")
            .Append("Oluşturma (UTC): ").Append(now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n\r\n")
            .Append("Dosyalar UTF-8, ayraç noktalı virgül (;), ondalık ayraç virgül, tarihler ISO 8601 (saatler UTC).\r\n")
            .Append("Silinmiş kayıtlar ve analiz/öneri gibi senin verinden türetilen sonuçlar dahil değildir;\r\n")
            .Append("bunlar aynı kayıtlardan yeniden hesaplanır. Şifre ve güvenlik anahtarları hiçbir zaman dışa aktarılmaz.\r\n\r\n")
            .Append("İçerik:\r\n");
        foreach (var file in files)
        {
            var rows = file.Content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length - 1;
            text.Append("- ").Append(file.Name).Append(" (").Append(rows).Append(" satır)\r\n");
        }

        return text.ToString();
    }
}
