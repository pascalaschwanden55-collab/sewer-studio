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

    // Ablage der Faelle; Originale und fremde Dateien liegen ausserhalb, damit der Scan sie nicht findet.
    private string ScanWurzel => Path.Combine(_root, "Scan");

    private string Fallordner(string haltung)
    {
        var ordner = Path.Combine(ScanWurzel, haltung);
        Directory.CreateDirectory(ordner);
        File.WriteAllText(Path.Combine(ordner, $"{haltung}_protokoll.json"), "{}");
        return ordner;
    }

    private static TrainingCenterImportService Verteiler()
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
