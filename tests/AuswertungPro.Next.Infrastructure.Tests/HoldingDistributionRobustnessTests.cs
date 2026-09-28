using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Fehler- und Wiederholungsfaelle der Haltungsverteilung (Pruefung AP07, 28.09.2026):
/// Ein Teilfehler oder ein zweiter Lauf darf keine zusaetzlichen Kopien anlegen, und jede
/// ausgewaehlte Datei erscheint im Ergebnis.
/// </summary>
public sealed class HoldingDistributionRobustnessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewerstudio-robust-" + Guid.NewGuid().ToString("N"));
    private string Quelle => Path.Combine(_root, "quelle");
    private string Videos => Path.Combine(_root, "videos");
    private string Ziel => Path.Combine(_root, "ziel");

    public HoldingDistributionRobustnessTests()
    {
        Directory.CreateDirectory(Quelle);
        Directory.CreateDirectory(Videos);
        Directory.CreateDirectory(Ziel);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Gesperrtes_Video_meldet_das_schon_abgelegte_PDF_und_die_Wiederholung_legt_es_nicht_doppelt_an()
    {
        var pdf = Pdf("bericht.pdf", "Haltungsinspektion - 12.07.2026 - 1000-2000");
        var video = Path.Combine(Videos, "20260712_1000-2000.mpg");
        File.WriteAllText(video, "video");

        HoldingFolderDistributor.DistributionResult erster;
        using (new FileStream(video, FileMode.Open, FileAccess.Read, FileShare.None))
            erster = Assert.Single(HoldingFolderDistributor.DistributeFiles([pdf], Videos, Ziel));

        Assert.False(erster.Success, erster.Message);
        Assert.Contains("PDF bereits abgelegt", erster.Message);
        Assert.NotNull(erster.DestPdfPath);
        Assert.True(File.Exists(erster.DestPdfPath));

        var zweiter = Assert.Single(HoldingFolderDistributor.DistributeFiles([pdf], Videos, Ziel));

        Assert.True(zweiter.Success, zweiter.Message);
        Assert.Equal(erster.DestPdfPath, zweiter.DestPdfPath);
        Assert.Single(Directory.GetFiles(Path.Combine(Ziel, "1000-2000"), "*.pdf"));
        Assert.Single(Directory.GetFiles(Path.Combine(Ziel, "1000-2000"), "*.mpg"));
    }

    [Fact]
    public void Zweiter_unveraenderter_Lauf_legt_keine_weiteren_Kopien_an()
    {
        var pdf = Pdf("bericht.pdf", "Haltungsinspektion - 12.07.2026 - 1000-2000");
        File.WriteAllText(Path.Combine(Videos, "20260712_1000-2000.mpg"), "video");
        File.WriteAllText(Path.Combine(Quelle, "kataster.xtf"), "<TRANSFER/>");

        var erster = HoldingFolderDistributor.DistributeFiles([pdf], Videos, Ziel);
        var zweiter = HoldingFolderDistributor.DistributeFiles([pdf], Videos, Ziel);

        Assert.All(erster.Concat(zweiter), r => Assert.True(r.Success, r.Message));
        var alle = Directory.GetFiles(Ziel, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(new[] { "20260712_1000-2000.mpg", "20260712_1000-2000.pdf", "kataster.xtf" }, alle);
    }

    [Fact]
    public void Fehlende_und_fremde_ausgewaehlte_Dateien_erscheinen_als_Fehler_die_uebrigen_werden_verteilt()
    {
        var pdf = Pdf("bericht.pdf", "Haltungsinspektion - 12.07.2026 - 1000-2000");
        var fehlt = Path.Combine(Quelle, "weg.pdf");
        var text = Path.Combine(Quelle, "notiz.txt");
        File.WriteAllText(text, "keine PDF");

        var results = HoldingFolderDistributor.DistributeFiles([pdf, fehlt, text], Videos, Ziel);

        Assert.Contains(results, r => r.Success && r.SourcePdfPath == pdf);
        Assert.Contains(results, r => !r.Success && r.SourcePdfPath == fehlt && r.Message.Contains("nicht gefunden"));
        Assert.Contains(results, r => !r.Success && r.SourcePdfPath == text && r.Message.Contains("keine PDF"));
    }

    [Fact]
    public void Nur_fehlende_Auswahl_nennt_jede_Datei()
    {
        var fehlt = Path.Combine(Quelle, "weg.pdf");

        foreach (var results in new[]
                 {
                     HoldingFolderDistributor.DistributeFiles([fehlt], Videos, Ziel),
                     HoldingFolderDistributor.DistributeShaftFiles([fehlt], Ziel),
                     HoldingFolderDistributor.DistributeDichtheitFiles([fehlt], Ziel),
                 })
        {
            var result = Assert.Single(results);
            Assert.False(result.Success);
            Assert.Equal(fehlt, result.SourcePdfPath);
            Assert.Contains("nicht gefunden", result.Message);
        }
    }

    [Fact]
    public void Leere_Auswahl_meldet_wie_bisher_keine_gueltige_PDF()
    {
        var result = Assert.Single(HoldingFolderDistributor.DistributeFiles([], Videos, Ziel));
        Assert.False(result.Success);
        Assert.Equal("No valid PDF files selected.", result.Message);
    }

    [Fact]
    public void Fehler_bei_der_Quelldateisuche_wird_gemeldet_statt_verschluckt()
    {
        var suche = HoldingFolderDistributor.EnumerateSidecarFiles(
            Quelle,
            (_, muster) => muster == "*.m150"
                ? throw new UnauthorizedAccessException("Unterordner gesperrt")
                : []);

        var problem = Assert.Single(suche.Probleme);
        Assert.Contains("*.m150", problem);
        Assert.Contains("Unterordner gesperrt", problem);
    }

    private string Pdf(string name, params string[] lines)
    {
        var path = Path.Combine(Quelle, name);
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        var y = 780m;
        foreach (var line in lines)
        {
            page.AddText(line, 12, new PdfPoint(40, y), font);
            y -= 18;
        }
        File.WriteAllBytes(path, builder.Build());
        return path;
    }
}
