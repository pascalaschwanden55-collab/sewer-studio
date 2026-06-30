using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer ImportSummaryTextBuilder.
/// Sichert das Formatierungsverhalten der Summary- und Details-Texte.
/// </summary>
public class ImportSummaryTextBuilderTests
{
    private static ImportStats MakeStats(int found, int created, int updated,
        int errors = 0, int uncertain = 0, params string[] messages)
        => new(found, created, updated, errors, uncertain, messages);

    [Fact]
    public void BuildSummary_EnthaltLabel()
    {
        var stats = MakeStats(5, 3, 2);
        var result = ImportSummaryTextBuilder.BuildSummary("WinCan", stats, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Contains("WinCan", result);
    }

    [Fact]
    public void BuildSummary_EnthaltGefundenUndNeu()
    {
        var stats = MakeStats(10, 7, 3);
        var result = ImportSummaryTextBuilder.BuildSummary("IBAK", stats, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Contains("Gefunden 10", result);
        Assert.Contains("Neu 7", result);
        Assert.Contains("Aktualisiert 3", result);
    }

    [Fact]
    public void BuildSummary_EnthaltXtfUndPdfZeile()
    {
        var stats = MakeStats(0, 0, 0);
        var result = ImportSummaryTextBuilder.BuildSummary(
            "WinCan", stats,
            xtfFiles: 2, xtfFound: 10, xtfUpdated: 5, xtfUncertain: 1, xtfErrors: 0,
            pdfFiles: 3, pdfFound: 8, pdfUpdated: 4, pdfUncertain: 0, pdfErrors: 1);

        Assert.Contains("XTF", result);
        Assert.Contains("PDF", result);
        Assert.Contains("Dateien 2", result);
        Assert.Contains("Dateien 3", result);
    }

    [Fact]
    public void BuildSummary_ImportquelleInMessages_WirdVorangestellt()
    {
        var stats = MakeStats(1, 1, 0, messages: "Importquelle: Testquelle");
        var result = ImportSummaryTextBuilder.BuildSummary("XTF", stats, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        // Importquelle-Zeile soll vor der naechsten Zeile erscheinen
        Assert.StartsWith("Importquelle:", result.TrimStart());
    }

    [Fact]
    public void BuildDetails_KombiniertSidecarUndQuelle()
    {
        var sidecar = new[] { "XTF: 5 Haltungen", "PDF: 2 Dateien" };
        var source = new[] { "WinCan: OK" };
        var result = ImportSummaryTextBuilder.BuildDetails(sidecar, source);

        Assert.Contains("XTF: 5 Haltungen", result);
        Assert.Contains("PDF: 2 Dateien", result);
        Assert.Contains("WinCan: OK", result);
    }

    [Fact]
    public void BuildDetails_BegrenztAuf200Zeilen()
    {
        var viele = Enumerable.Range(0, 300).Select(i => $"Zeile {i}").ToArray();
        var result = ImportSummaryTextBuilder.BuildDetails(viele, Array.Empty<string>());
        var zeilen = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(200, zeilen.Length);
    }

    [Fact]
    public void BuildDetails_LeereListen_GibtLeerStringZurueck()
    {
        var result = ImportSummaryTextBuilder.BuildDetails(
            Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal("", result);
    }
}
