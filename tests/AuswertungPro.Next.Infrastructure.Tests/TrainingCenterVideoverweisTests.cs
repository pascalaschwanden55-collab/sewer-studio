using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// PR #85 (Codex-Hinweis P1): Seit Deepscan R6 legt die Haltungsverteilung statt einer symbolischen
/// Verknuepfung nur noch <c>&lt;Video&gt;.link</c> ab. Der Scan muss diesen Verweis nur lesend zum
/// Originalvideo aufloesen, sonst laden neu verteilte Faelle ohne Video.
/// </summary>
public sealed class TrainingCenterVideoverweisTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-trainingcenter-videoverweis-" + Guid.NewGuid().ToString("N"));

    public TrainingCenterVideoverweisTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
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

    [Fact]
    public async Task Verteilen_dann_scannen_liefert_das_originalvideo()
    {
        var videos = Path.Combine(_root, "Videos");
        Directory.CreateDirectory(videos);
        var video = Path.Combine(videos, "H_23021-22369.mpg");
        File.WriteAllText(video, "kunden-video");
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        var dienst = Verteiler();
        await dienst.DistributeByHaltungAsync(Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);

        // Ohne Liste wie Batch-Import und Selbsttraining: auch diese Leser erhalten das Video.
        var fall = Assert.Single(await dienst.ScanAsync(ausgabe));

        Assert.Equal(video, fall.VideoPath);
        Assert.EndsWith("23021-22369_protokoll.json", fall.ProtocolPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verweis_auf_fehlendes_video_laedt_den_fall_ohne_video_und_nennt_den_verweis()
    {
        var fallordner = Fallordner("23021-22369");
        var verweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.WriteAllText(verweis, Path.Combine(_root, "fehlt", "H_23021-22369.mpg"));
        var hinweise = new List<string>();

        var fall = Assert.Single(await new TrainingCenterImportService().ScanAsync(
            ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal("", fall.VideoPath);
        var hinweis = Assert.Single(hinweise);
        Assert.Contains(verweis, hinweis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verweis_auf_eine_nicht_videodatei_wird_nicht_uebernommen()
    {
        var fallordner = Fallordner("23021-22369");
        var fremd = Path.Combine(_root, "geheim.txt");
        File.WriteAllText(fremd, "kein video");
        File.WriteAllText(Path.Combine(fallordner, "H_23021-22369.mpg.link"), fremd);
        var hinweise = new List<string>();

        var fall = Assert.Single(await new TrainingCenterImportService().ScanAsync(
            ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal("", fall.VideoPath);
        Assert.Single(hinweise);
    }

    [Fact]
    public async Task Unlesbarer_verweis_wird_gemeldet_statt_verschluckt()
    {
        var fallordner = Fallordner("23021-22369");
        var verweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.WriteAllText(verweis, Path.Combine(_root, "egal.mpg"));
        var hinweise = new List<string>();

        List<TrainingCaseInput> faelle;
        using (new FileStream(verweis, FileMode.Open, FileAccess.Read, FileShare.None))
            faelle = await new TrainingCenterImportService().ScanAsync(ScanWurzel, null, hinweise, CancellationToken.None);

        Assert.Equal("", Assert.Single(faelle).VideoPath);
        var hinweis = Assert.Single(hinweise);
        Assert.Contains(verweis, hinweis, StringComparison.Ordinal);
        Assert.Contains("nicht lesbar", hinweis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verweis_mit_reparse_attribut_wird_nicht_gelesen_sondern_gemeldet()
    {
        // PR #85 (Codex-Hinweis P2): Eine .link-Datei, die selbst eine Verknuepfung ist, wuerde beim Lesen
        // aus dem Baum herausfuehren. Attribut-Testnaht, damit der Beleg ohne Symlink-Recht laeuft.
        var fallordner = Fallordner("23021-22369");
        var original = Path.Combine(_root, "H_23021-22369.mpg");
        File.WriteAllText(original, "original");
        var verweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.WriteAllText(verweis, original);
        var hinweise = new List<string>();
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: null,
            dateienImOrdner: null,
            nachHaltungsordner: null,
            leseAttribute: pfad => string.Equals(pfad, verweis, StringComparison.OrdinalIgnoreCase)
                ? FileAttributes.Archive | FileAttributes.ReparsePoint
                : File.GetAttributes(pfad));

        var fall = Assert.Single(await dienst.ScanAsync(ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal("", fall.VideoPath);
        var hinweis = Assert.Single(hinweise);
        Assert.Contains(verweis, hinweis, StringComparison.Ordinal);
        Assert.Contains("Verknüpfung", hinweis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nicht_pruefbarer_verweis_wird_abgelehnt()
    {
        var fallordner = Fallordner("23021-22369");
        var original = Path.Combine(_root, "H_23021-22369.mpg");
        File.WriteAllText(original, "original");
        var verweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.WriteAllText(verweis, original);
        var hinweise = new List<string>();
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: null,
            dateienImOrdner: null,
            nachHaltungsordner: null,
            leseAttribute: pfad => string.Equals(pfad, verweis, StringComparison.OrdinalIgnoreCase)
                ? throw new UnauthorizedAccessException("gesperrt")
                : File.GetAttributes(pfad));

        var fall = Assert.Single(await dienst.ScanAsync(ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal("", fall.VideoPath);
        Assert.Contains(verweis, Assert.Single(hinweise), StringComparison.Ordinal);
    }

    [JunctionFact]
    public async Task Verweis_als_echter_datei_symlink_auf_fremde_datei_wird_nicht_gelesen()
    {
        var fallordner = Fallordner("23021-22369");
        var original = Path.Combine(_root, "H_23021-22369.mpg");
        File.WriteAllText(original, "original");
        var fremd = Path.Combine(_root, "fremd.txt");
        File.WriteAllText(fremd, original);
        var verweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.CreateSymbolicLink(verweis, fremd);
        var hinweise = new List<string>();

        var fall = Assert.Single(await new TrainingCenterImportService().ScanAsync(
            ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal("", fall.VideoPath);
        Assert.Contains(verweis, Assert.Single(hinweise), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Echtes_video_im_ordner_hat_vorrang_vor_dem_verweis()
    {
        var fallordner = Fallordner("23021-22369");
        var echt = Path.Combine(fallordner, "H_23021-22369.mp4");
        File.WriteAllText(echt, "video im ordner");
        var original = Path.Combine(_root, "H_23021-22369.mpg");
        File.WriteAllText(original, "original");
        File.WriteAllText(Path.Combine(fallordner, "H_23021-22369.mpg.link"), original);
        var hinweise = new List<string>();

        var fall = Assert.Single(await new TrainingCenterImportService().ScanAsync(
            ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal(echt, fall.VideoPath);
        Assert.Empty(hinweise);
    }

    // --- PR #85: Video-Symlinks aelterer Verteillaeufe (Entscheid Koordinator 03.10.2026) ---

    [Fact]
    public async Task Scan_nimmt_verknuepftes_video_nicht_und_nutzt_den_link_verweis()
    {
        var fallordner = Fallordner("23021-22369");
        var original = Path.Combine(_root, "H_23021-22369.mpg");
        File.WriteAllText(original, "original");
        var alterSymlink = Path.Combine(fallordner, "H_23021-22369.mpg");
        File.WriteAllText(alterSymlink, "steht fuer einen alten Symlink");
        File.WriteAllText(alterSymlink + ".link", original);
        var hinweise = new List<string>();

        var fall = Assert.Single(await DienstMitVerknuepfung(alterSymlink).ScanAsync(
            ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal(original, fall.VideoPath);
        var hinweis = Assert.Single(hinweise);
        Assert.Contains($"Video «{alterSymlink}» ist eine Verknüpfung", hinweis, StringComparison.Ordinal);
        Assert.Contains("Verteilung erneut ausführen", hinweis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scan_ohne_link_verweis_laedt_den_fall_ohne_das_verknuepfte_video()
    {
        var fallordner = Fallordner("23021-22369");
        var alterSymlink = Path.Combine(fallordner, "H_23021-22369.mpg");
        File.WriteAllText(alterSymlink, "steht fuer einen alten Symlink");
        var hinweise = new List<string>();

        var fall = Assert.Single(await DienstMitVerknuepfung(alterSymlink).ScanAsync(
            ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal("", fall.VideoPath);
        Assert.Contains(alterSymlink, Assert.Single(hinweise), StringComparison.Ordinal);
    }

    [JunctionFact]
    public async Task Scan_nimmt_echten_video_symlink_nicht_und_nutzt_den_link_verweis()
    {
        var fallordner = Fallordner("23021-22369");
        var original = Path.Combine(_root, "H_23021-22369.mpg");
        File.WriteAllText(original, "original");
        var fremd = Path.Combine(_root, "fremd.mpg");
        File.WriteAllText(fremd, "fremd");
        var alterSymlink = Path.Combine(fallordner, "H_23021-22369.mpg");
        File.CreateSymbolicLink(alterSymlink, fremd);
        File.WriteAllText(alterSymlink + ".link", original);
        var hinweise = new List<string>();

        var fall = Assert.Single(await new TrainingCenterImportService().ScanAsync(
            ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal(original, fall.VideoPath);
        Assert.Contains(alterSymlink, Assert.Single(hinweise), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verteilung_zaehlt_verknuepftes_altes_video_nicht_und_schreibt_den_link_verweis()
    {
        var videos = Path.Combine(_root, "Videos");
        Directory.CreateDirectory(videos);
        var video = Path.Combine(videos, "H_23021-22369.mpg");
        File.WriteAllText(video, "kunden-video");
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        var fallordner = Path.Combine(ausgabe, "23021-22369");
        Directory.CreateDirectory(fallordner);
        var alterSymlink = Path.Combine(fallordner, "H_23021-22369.mpg");
        File.WriteAllText(alterSymlink, "steht fuer einen alten Symlink");

        var ergebnis = await Verteiler(VerknuepfungFuer(alterSymlink)).DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);

        Assert.Equal(1, ergebnis.VideosMatched);
        Assert.Equal(video, File.ReadAllText(alterSymlink + ".link"));
        Assert.Equal("steht fuer einen alten Symlink", File.ReadAllText(alterSymlink));
    }

    [JunctionFact]
    public async Task Verteilung_laesst_echten_alten_video_symlink_stehen_und_schreibt_den_link_verweis()
    {
        var videos = Path.Combine(_root, "Videos");
        Directory.CreateDirectory(videos);
        var video = Path.Combine(videos, "H_23021-22369.mpg");
        File.WriteAllText(video, "kunden-video");
        var fremd = Path.Combine(_root, "fremd.mpg");
        File.WriteAllText(fremd, "fremd");
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        var fallordner = Path.Combine(ausgabe, "23021-22369");
        Directory.CreateDirectory(fallordner);
        var alterSymlink = Path.Combine(fallordner, "H_23021-22369.mpg");
        File.CreateSymbolicLink(alterSymlink, fremd);

        var ergebnis = await Verteiler().DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);

        Assert.Equal(1, ergebnis.VideosMatched);
        Assert.Equal(video, File.ReadAllText(alterSymlink + ".link"));
        Assert.Equal(fremd, new FileInfo(alterSymlink).LinkTarget);
        Assert.Equal("fremd", File.ReadAllText(fremd));
        var fall = Assert.Single(await Verteiler().ScanAsync(ausgabe));
        Assert.Equal(video, fall.VideoPath);
    }

    private static Func<string, FileAttributes?> VerknuepfungFuer(string pfad)
        => p => string.Equals(p, pfad, StringComparison.OrdinalIgnoreCase)
            ? FileAttributes.Archive | FileAttributes.ReparsePoint
            : File.GetAttributes(p);

    private static TrainingCenterImportService DienstMitVerknuepfung(string pfad)
        => new(pdfSeitenLesen: null, dateienImOrdner: null, nachHaltungsordner: null, leseAttribute: VerknuepfungFuer(pfad));

    // Ablage der Faelle; Originale und fremde Dateien liegen ausserhalb, damit der Scan sie nicht findet.
    private string ScanWurzel => Path.Combine(_root, "Scan");

    private string Fallordner(string haltung)
    {
        var ordner = Path.Combine(ScanWurzel, haltung);
        Directory.CreateDirectory(ordner);
        File.WriteAllText(Path.Combine(ordner, $"{haltung}_protokoll.json"), "{}");
        return ordner;
    }

    private static TrainingCenterImportService Verteiler(Func<string, FileAttributes?>? leseAttribute = null)
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
            nachHaltungsordner: null,
            leseAttribute: leseAttribute);
}
