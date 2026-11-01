using System.Globalization;

namespace Nirengi.BuildingBlocks.Application.Export;

/// <summary>One file of the personal data export (KVKK data portability, ADR-0018).</summary>
/// <param name="Name">File name inside the archive, e.g. <c>konu_testleri.csv</c>.</param>
/// <param name="Rows">Data records, header excluded (a quoted value may span several lines).</param>
public sealed record ExportFile(string Name, string Content, int Rows);

/// <summary>
/// Implemented by every module that holds data the student entered or that describes them. Platform collects all
/// implementations without knowing the modules; a module exports only from its own schema.
/// </summary>
public interface IPersonalDataExporter
{
    /// <summary>Files appear in this order, then by name.</summary>
    int Order { get; }

    Task<IReadOnlyList<ExportFile>> Export(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// CSV for Turkish spreadsheets: <c>;</c> separator, decimal comma, ISO dates, yes/no in Turkish. The archive
/// writer adds the UTF-8 BOM so Excel detects the encoding.
/// </summary>
public sealed class CsvTable(params string[] headers)
{
    private readonly List<string> _lines = [string.Join(';', headers.Select(Escape))];

    public CsvTable Add(params object?[] values)
    {
        if (values.Length != headers.Length)
        {
            throw new ArgumentException($"Expected {headers.Length} values, got {values.Length}.", nameof(values));
        }

        _lines.Add(string.Join(';', values.Select(v => Escape(Format(v)))));
        return this;
    }

    public ExportFile ToFile(string name) => new(name, string.Join("\r\n", _lines) + "\r\n", _lines.Count - 1);

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        bool b => b ? "evet" : "hayır",
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset t => t.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ','),
        double x => x.ToString("0.####", CultureInfo.InvariantCulture).Replace('.', ','),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string Escape(string value)
    {
        // A leading =, +, -, @, tab or carriage return can start a formula in a spreadsheet (CSV injection, OWASP).
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0], StringComparison.Ordinal) && !double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([';', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }
}
