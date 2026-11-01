using Nirengi.BuildingBlocks.Application.Export;
using Shouldly;

namespace Nirengi.BuildingBlocks.UnitTests;

public sealed class CsvTableTests
{
    [Fact]
    public void Formats_values_for_turkish_spreadsheets()
    {
        var file = new CsvTable("tarih", "net", "oran", "aktif", "zaman", "bos")
            .Add(new DateOnly(2026, 11, 1), 12.75m, 0.5, true, new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(3)), null)
            .ToFile("a.csv");

        file.Content.ShouldBe("tarih;net;oran;aktif;zaman;bos\r\n2026-11-01;12,75;0,5;evet;2026-11-01T09:00:00Z;\r\n");
    }

    [Fact]
    public void Quotes_separators_and_defuses_formulas()
    {
        var content = new CsvTable("not").Add("a;b \"c\"").Add("=HYPERLINK(1)").Add("-3,5").ToFile("n.csv").Content;

        content.ShouldBe("not\r\n\"a;b \"\"c\"\"\"\r\n'=HYPERLINK(1)\r\n-3,5\r\n");
    }

    [Theory]
    [InlineData("\t=1+1")]
    [InlineData("\r=1+1")]
    [InlineData("@SUM(A1)")]
    public void Defuses_formulas_behind_tab_and_carriage_return(string value) =>
        new CsvTable("not").Add(value).ToFile("n.csv").Content.ShouldContain("'" + value);

    [Fact]
    public void Counts_records_not_lines()
    {
        var file = new CsvTable("not").Add("iki\r\nsatır").Add("tek").ToFile("n.csv");

        file.Rows.ShouldBe(2);
    }

    [Fact]
    public void Row_width_must_match_the_header() =>
        Should.Throw<ArgumentException>(() => new CsvTable("a", "b").Add(1));
}
