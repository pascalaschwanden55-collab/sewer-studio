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
    public async Task DistributeByHaltungAsync_verknuepfung_oberhalb_des_ausgabeordners_wird_ohne_schreiben_abgelehnt()
    {
        // PR #85 (Codex-Hinweis P1): Das PDF liegt unter einer Verknuepfung; der abgeleitete
        // Ausgabeordner <Eltern des PDF-Ordners>\<PDF-Name>_Training laege damit im Verknuepfungsziel.
        var echt = Path.Combine(_root, "Echt");
        Directory.CreateDirectory(Path.Combine(echt, "PDFs"));
        var kundenLink = Path.Combine(_root, "KundenLink");
        Verknuepfe(kundenLink, echt);
        var ausgabe = Path.Combine(kundenLink, "Sammel_Training");

        var ergebnis = await Dienst().DistributeByHaltungAsync(
            Path.Combine(kundenLink, "PDFs", "Sammel.pdf"), _root, ausgabe, CancellationToken.None);

        Assert.Equal(0, ergebnis.Distributed);
        Assert.Equal(["PDFs"], Directory.EnumerateFileSystemEntries(echt).Select(Path.GetFileName));
        Assert.Contains(ergebnis.Messages, meldung =>
            meldung.Contains("wird nicht beschrieben", StringComparison.Ordinal)
            && meldung.Contains(kundenLink, StringComparison.OrdinalIgnoreCase)
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

    [JunctionFact]
    public async Task DistributeByHaltungAsync_abgelehnter_videoverweis_zaehlt_nicht_als_zugeordnetes_video()
    {
        // PR #85 (Codex-Hinweis P2): Zaehler und Erfolgsmeldung nur nach geschriebenem Verweis.
        var videos = Path.Combine(_root, "Videos");
        Directory.CreateDirectory(videos);
        File.WriteAllText(Path.Combine(videos, "H_23021-22369.mpg"), "kunden-video");
        var fremd = Path.Combine(_root, "Fremd");
        Directory.CreateDirectory(fremd);
        var fallordner = Path.Combine(_root, "Sammel_Training", "23021-22369");
        Directory.CreateDirectory(fallordner);
        Verknuepfe(Path.Combine(fallordner, "H_23021-22369.mpg.link"), fremd);

        var ergebnis = await Dienst().DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), videos, Path.Combine(_root, "Sammel_Training"), CancellationToken.None);

        Assert.Equal(0, ergebnis.VideosMatched);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fremd));
        Assert.Contains(ergebnis.Messages, meldung =>
            meldung.StartsWith("Haltung 23021-22369: Videoverweis", StringComparison.Ordinal));
        Assert.DoesNotContain(ergebnis.Messages, meldung => meldung.Contains(", Video:", StringComparison.Ordinal));
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

    // --- PR #85, Runde 6: Videoordner, Videoziel, Abbruch, Fehlertext ---

    [JunctionFact]
    public async Task DistributeByHaltungAsync_nennt_verknuepften_unterordner_des_videoordners()
    {
        var videos = Path.Combine(_root, "Videos");
        Directory.CreateDirectory(videos);
        var fremd = Path.Combine(_root, "Fremd");
        Directory.CreateDirectory(fremd);
        File.WriteAllText(Path.Combine(fremd, "H_23021-22369.mpg"), "fremdes video");
        var link = Path.Combine(videos, "Unterordner");
        Verknuepfe(link, fremd);

        var ergebnis = await Dienst().DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), videos, Path.Combine(_root, "Sammel_Training"), CancellationToken.None);

        Assert.Equal(0, ergebnis.VideosMatched);
        Assert.Contains(ergebnis.Messages, meldung =>
            meldung.StartsWith($"Ordner «{link}» übersprungen", StringComparison.Ordinal));
    }

    [JunctionFact]
    public async Task DistributeByHaltungAsync_nennt_verknuepfte_videodatei_im_videoordner()
    {
        var videos = Path.Combine(_root, "Videos");
        Directory.CreateDirectory(videos);
        var fremd = Path.Combine(_root, "fremd.mpg");
        File.WriteAllText(fremd, "fremdes video");
        var videoLink = Path.Combine(videos, "H_23021-22369.mpg");
        File.CreateSymbolicLink(videoLink, fremd);

        var ergebnis = await Dienst().DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), videos, Path.Combine(_root, "Sammel_Training"), CancellationToken.None);

        Assert.Equal(0, ergebnis.VideosMatched);
        Assert.Contains(ergebnis.Messages, meldung =>
            meldung.StartsWith($"Video «{videoLink}» übersprungen", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DistributeByHaltungAsync_schreibt_keinen_verweis_auf_ein_video_unter_einer_verknuepfung()
    {
        var videos = Path.Combine(_root, "Videos");
        Directory.CreateDirectory(videos);
        File.WriteAllText(Path.Combine(videos, "H_23021-22369.mpg"), "kunden-video");
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: _ => new PdfTextExtraction([Protokollseite()], ""),
            dateienImOrdner: null,
            nachHaltungsordner: null,
            leseAttribute: pfad => string.Equals(pfad, videos, StringComparison.OrdinalIgnoreCase)
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : File.GetAttributes(pfad));

        var ergebnis = await dienst.DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);

        Assert.Equal(0, ergebnis.VideosMatched);
        Assert.False(File.Exists(Path.Combine(ausgabe, "23021-22369", "H_23021-22369.mpg.link")));
        Assert.Contains(ergebnis.Messages, meldung =>
            meldung.StartsWith("Haltung 23021-22369: Video «", StringComparison.Ordinal)
            && meldung.Contains("Verknüpfung", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DistributeByHaltungAsync_abbruch_vor_dem_videoindex_legt_keinen_ausgabeordner_an()
    {
        using var abbruch = new CancellationTokenSource();
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: _ =>
            {
                abbruch.Cancel();
                return new PdfTextExtraction([Protokollseite()], "");
            },
            dateienImOrdner: null,
            nachHaltungsordner: null);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dienst.DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), _root, ausgabe, abbruch.Token));

        Assert.False(Directory.Exists(ausgabe));
    }

    [Fact]
    public async Task DistributeByHaltungAsync_pdf_fehler_zeigt_keinen_rohen_ausnahmetext()
    {
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: _ => throw new IOException(@"Zugriff auf C:\Geheim\Sammel.pdf verweigert"),
            dateienImOrdner: null,
            nachHaltungsordner: null);

        var ergebnis = await dienst.DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), _root, Path.Combine(_root, "Sammel_Training"), CancellationToken.None);

        var meldung = Assert.Single(ergebnis.Messages);
        Assert.StartsWith("PDF-Text konnte nicht extrahiert werden: Eine Datei oder ein Ordner ist momentan nicht verfügbar", meldung, StringComparison.Ordinal);
        Assert.DoesNotContain("Geheim", meldung, StringComparison.Ordinal);
    }

    private static string Protokollseite()
        => string.Join("\n",
        [
            "Kanalfernsehprotokoll / Inspektion: 1",
            "Haltungsname:                Datum :                Wetter :               Operator :",
            " 23021-22369                22.04.2014          schoen_trocken           Manuel Joschko"
        ]);

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
