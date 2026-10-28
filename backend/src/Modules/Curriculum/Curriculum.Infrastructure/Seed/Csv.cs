using System.Text;

namespace Nirengi.Modules.Curriculum.Infrastructure.Seed;

/// <summary>Minimal RFC 4180 reader: quoted fields, doubled quotes, CRLF or LF. Enough for the seed files.</summary>
internal static class Csv
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Read(string text)
    {
        var rows = ParseRows(text);
        if (rows.Count == 0)
        {
            return [];
        }

        var header = rows[0];
        var records = new List<IReadOnlyDictionary<string, string>>(rows.Count - 1);
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Count == 1 && row[0].Length == 0)
            {
                continue;
            }

            if (row.Count != header.Count)
            {
                throw new FormatException($"CSV row {i + 1} has {row.Count} fields, header has {header.Count}.");
            }

            var record = new Dictionary<string, string>(header.Count, StringComparer.Ordinal);
            for (var c = 0; c < header.Count; c++)
            {
                record[header[c]] = row[c];
            }

            records.Add(record);
        }

        return records;
    }

    private static List<List<string>> ParseRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch != '"')
                {
                    field.Append(ch);
                }
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
