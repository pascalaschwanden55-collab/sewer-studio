using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Deepscan 02.10.2026, R6: Die Haltungsverteilung des Training Centers schreibt neben die
/// Kundenablage (&lt;Eltern&gt;\&lt;PDF-Name&gt;_Training). Vor jedem Schreiben gilt der Verteil-Pfadwaechter;
/// ein verknuepftes Ziel wird ohne Schreiben abgelehnt. Eine symbolische Verknuepfung auf das
/// Video wird nicht mehr versucht, der Verweis steht immer in einer .link-Datei.
/// </summary>
public sealed class TrainingCenterImportServiceVerknuepfungTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-trainingcenter-verknuepfung-" + Guid.NewGuid().ToString("N"));

    private readonly List<string> _links = new();

    public TrainingCenterImportServiceVerknuepfungTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        foreach (var link in _links)
        {
            try
            {
                if (Directory.Exists(link))
                    Directory.Delete(link);
            }
            catch (IOException)
            {
                // Test-Aufraeumen darf das Ergebnis nicht verdecken.
            }
        }

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }
    }

    [JunctionFact]
    public async Task DistributeByHaltungAsync_verknuepfter_ausgabeordner_wird_ohne_schreiben_abgelehnt()
    {
        var fremd = Path.Combine(_root, "Fremd");
        Directory.CreateDirectory(fremd);
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        Verknuepfe(ausgabe, fremd);

        var ergebnis = await Dienst().DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), _root, ausgabe, CancellationToken.None);

        Assert.Equal(0, ergebnis.Distributed);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fremd));
        Assert.Contains(ergebnis.Messages, meldung =>
            meldung.Contains("wird nicht beschrieben", StringComparison.Ordinal)
            && meldung.Contains("Verknüpfung", StringComparison.Ordinal));
    }

    [JunctionFact]
    public async Task DistributeByHaltungAsync_verknuepfter_haltungsordner_wird_nicht_beschrieben()
    {
        var fremd = Path.Combine(_root, "Fremd");
        Directory.CreateDirectory(fremd);
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        Directory.CreateDirectory(ausgabe);
        Verknuepfe(Path.Combine(ausgabe, "23021-22369"), fremd);

        var ergebnis = await Dienst().DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), _root, ausgabe, CancellationToken.None);

        Assert.Equal(0, ergebnis.Distributed);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fremd));
        Assert.Contains(ergebnis.Messages, meldung =>
            meldung.StartsWith("Haltung 23021-22369:", StringComparison.Ordinal)
            && meldung.Contains("nicht beschrieben", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DistributeByHaltungAsync_legt_keine_symbolische_verknuepfung_an_sondern_eine_link_datei()
    {
        var videos = Path.Combine(_root, "Videos");
        Directory.CreateDirectory(videos);
        var video = Path.Combine(videos, "H_23021-22369.mpg");
        File.WriteAllText(video, "kunden-video");
        var ausgabe = Path.Combine(_root, "Sammel_Training");

        var ergebnis = await Dienst().DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);

        var fallordner = Path.Combine(ausgabe, "23021-22369");
        Assert.Equal(1, ergebnis.VideosMatched);
        Assert.False(File.Exists(Path.Combine(fallordner, "H_23021-22369.mpg")));
        Assert.Equal(video, File.ReadAllText(Path.Combine(fallordner, "H_23021-22369.mpg.link")));
    }

    private void Verknuepfe(string link, string ziel)
    {
        JunctionTestSupport.CreateDirectoryLink(link, ziel);
        _links.Add(link);
    }

    private static TrainingCenterImportService Dienst()
        => new(
            pdfSeitenLesen: _ => new PdfTextExtraction(
                [string.Join("\n",
                [
                    "Kanalfernsehprotokoll / Inspektion: 1",
                    "Haltungsname:                Datum :                Wetter :               Operator :",
                    " 23021-22369                22.04.2014          schoen_trocken           Manuel Joschko"
                ])],
                ""),
            dateienImOrdner: null,
            nachHaltungsordner: null);
}
