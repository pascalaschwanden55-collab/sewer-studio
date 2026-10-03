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

    // Review PR #85: Ein syntaktisch ungueltiger, aber voll qualifizierter Zielpfad (NUL-Zeichen) darf
    // nicht den ganzen Fall verwerfen; nur der Verweis wird abgelehnt, das Protokoll laedt ohne Video.
    [Fact]
    public async Task Verweis_mit_ungueltigem_zielpfad_laedt_den_fall_ohne_video()
    {
        var fallordner = Fallordner("23021-22369");
        var verweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.WriteAllText(verweis, Path.Combine(_root, "Vid\0eo", "H_23021-22369.mpg"));
        var hinweise = new List<string>();

        var fall = Assert.Single(await new TrainingCenterImportService().ScanAsync(
            ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal("", fall.VideoPath);
        Assert.Contains(hinweise, hinweis => hinweis.Contains(verweis, StringComparison.Ordinal));
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
    public async Task Verweisziel_das_eine_verknuepfung_ist_wird_nicht_uebernommen()
    {
        // PR #85: Ist das Originalvideo inzwischen selbst ein Datei-Symlink, ginge der verknuepfte Pfad an FFmpeg.
        var fallordner = Fallordner("23021-22369");
        var original = Path.Combine(_root, "H_23021-22369.mpg");
        File.WriteAllText(original, "steht fuer einen Symlink");
        var verweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.WriteAllText(verweis, original);
        var hinweise = new List<string>();

        var fall = Assert.Single(await DienstMitVerknuepfung(original).ScanAsync(
            ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal("", fall.VideoPath);
        var hinweis = Assert.Single(hinweise);
        Assert.Contains(verweis, hinweis, StringComparison.Ordinal);
        Assert.Contains(original, hinweis, StringComparison.Ordinal);
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

    // --- PR #85, Runde 7: erneutes Verteilen mit anderem Video ---

    [Fact]
    public async Task Erneute_verteilung_mit_anderem_video_ersetzt_den_alten_verweis()
    {
        var (videos, ausgabe, fallordner, altVideo) = await ErsteVerteilungMitMpgAsync();
        var neuVideo = Path.Combine(videos, "H_23021-22369.mp4");
        File.WriteAllText(neuVideo, "neues video");

        var ergebnis = await Verteiler().DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);

        Assert.Equal(1, ergebnis.VideosMatched);
        Assert.Equal(["H_23021-22369.mp4.link"], Directory.GetFiles(fallordner, "*.link").Select(Path.GetFileName));
        Assert.True(File.Exists(altVideo));
        var fall = Assert.Single(await Verteiler().ScanAsync(ausgabe));
        Assert.Equal(neuVideo, fall.VideoPath);
    }

    [Fact]
    public async Task Alter_verweis_nicht_loeschbar_wird_gemeldet_und_der_scan_koppelt_nicht_falsch()
    {
        var (videos, ausgabe, fallordner, _) = await ErsteVerteilungMitMpgAsync();
        var alterVerweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.SetAttributes(alterVerweis, FileAttributes.ReadOnly);
        File.WriteAllText(Path.Combine(videos, "H_23021-22369.mp4"), "neues video");
        try
        {
            var ergebnis = await Verteiler().DistributeByHaltungAsync(
                Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);

            Assert.True(File.Exists(alterVerweis));
            Assert.Contains(ergebnis.Messages, meldung =>
                meldung.Contains($"«{alterVerweis}» konnte nicht entfernt werden", StringComparison.Ordinal));
            // Review PR #85: Bleibt ein alter Verweis liegen, laedt der Scan den Fall ohne Video – dann darf
            // die Verteilung auch keinen Videotreffer melden.
            Assert.Equal(0, ergebnis.VideosMatched);
            Assert.DoesNotContain(ergebnis.Messages, meldung => meldung.Contains(", Video:", StringComparison.Ordinal));
            var hinweise = new List<string>();
            var fall = Assert.Single(await Verteiler().ScanAsync(ausgabe, null, hinweise, CancellationToken.None));
            Assert.Equal("", fall.VideoPath);
            Assert.Contains(hinweise, hinweis => hinweis.Contains("mehrere Videoverweise", StringComparison.Ordinal));
        }
        finally
        {
            File.SetAttributes(alterVerweis, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task Alter_verweis_als_verknuepfung_wird_nicht_angefasst_sondern_gemeldet()
    {
        var (videos, ausgabe, fallordner, _) = await ErsteVerteilungMitMpgAsync();
        var alterVerweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.WriteAllText(Path.Combine(videos, "H_23021-22369.mp4"), "neues video");

        var ergebnis = await Verteiler(VerknuepfungFuer(alterVerweis)).DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);

        Assert.True(File.Exists(alterVerweis));
        Assert.Contains(ergebnis.Messages, meldung =>
            meldung.Contains($"«{alterVerweis}» ist eine Verknüpfung", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mehrere_gueltige_verweise_im_fallordner_werden_nicht_geraten()
    {
        var fallordner = Fallordner("23021-22369");
        var a = Path.Combine(_root, "H_23021-22369.mpg");
        var b = Path.Combine(_root, "H_23021-22369.mp4");
        File.WriteAllText(a, "a");
        File.WriteAllText(b, "bb");
        File.WriteAllText(Path.Combine(fallordner, "H_23021-22369.mpg.link"), a);
        File.WriteAllText(Path.Combine(fallordner, "H_23021-22369.mp4.link"), b);
        var hinweise = new List<string>();

        var fall = Assert.Single(await new TrainingCenterImportService().ScanAsync(
            ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal("", fall.VideoPath);
        var hinweis = Assert.Single(hinweise);
        Assert.Contains("mehrere Videoverweise", hinweis, StringComparison.Ordinal);
        Assert.Contains("bitte die Verteilung erneut ausführen", hinweis, StringComparison.Ordinal);
    }

    // Review PR #85: Ein Fallordner, der sich beim Bereinigen nicht auflisten laesst, darf die Verteilung
    // nicht abbrechen; der Fehler wird gemeldet, und ohne eindeutige Bereinigung zaehlt kein Videotreffer.
    [Fact]
    public async Task Fallordner_nicht_auflistbar_bricht_die_verteilung_nicht_ab()
    {
        var (videos, ausgabe, fallordner, _) = await ErsteVerteilungMitMpgAsync();
        File.WriteAllText(Path.Combine(videos, "H_23021-22369.mp4"), "neues video");
        var dienst = Verteiler(dateienImOrdner: ordner =>
            string.Equals(ordner, fallordner, StringComparison.OrdinalIgnoreCase)
                ? throw new UnauthorizedAccessException("Zugriff verweigert")
                : Directory.EnumerateFiles(ordner, "*.*", SearchOption.TopDirectoryOnly));

        var ergebnis = await dienst.DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);

        Assert.Equal(1, ergebnis.Distributed);
        Assert.Equal(0, ergebnis.VideosMatched);
        Assert.Contains(ergebnis.Messages, meldung =>
            meldung.Contains("alte Videoverweise konnten nicht geprüft werden", StringComparison.Ordinal));
    }

    // Review PR #85: Ein Schreibfehler einer Haltung (hier der gleichnamige, schreibgeschuetzte Verweis)
    // wird fuer diese Haltung gemeldet und bricht die Verteilung nicht mit einer Ausnahme ab.
    [Fact]
    public async Task Schreibfehler_einer_haltung_wird_gemeldet_statt_die_verteilung_abzubrechen()
    {
        var (videos, ausgabe, fallordner, _) = await ErsteVerteilungMitMpgAsync();
        var verweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.SetAttributes(verweis, FileAttributes.ReadOnly);
        try
        {
            var ergebnis = await Verteiler().DistributeByHaltungAsync(
                Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);

            Assert.Equal(0, ergebnis.Distributed);
            Assert.Equal(0, ergebnis.VideosMatched);
            Assert.Contains(ergebnis.Messages, meldung =>
                meldung.Contains("Haltung 23021-22369: konnte nicht geschrieben werden", StringComparison.Ordinal));
        }
        finally
        {
            // Der Schreibbaustein legt beim Versuch eine (ebenfalls schreibgeschuetzte) .bak-Kopie an.
            foreach (var datei in Directory.EnumerateFiles(fallordner))
                File.SetAttributes(datei, FileAttributes.Normal);
        }
    }

    // Review PR #85: Verschwindet das indexierte Video vor der Verarbeitung der Haltung (Netzlaufwerk
    // getrennt, extern geloescht), wird kein defekter Verweis geschrieben und kein Videotreffer gezaehlt.
    [Fact]
    public async Task Verschwundenes_quellvideo_ergibt_keinen_verweis_und_keinen_treffer()
    {
        var videos = Path.Combine(_root, "Videos");
        Directory.CreateDirectory(videos);
        var video = Path.Combine(videos, "H_23021-22369.mpg");
        File.WriteAllText(video, "video");
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        FileAttributes? LeseUndLoesche(string pfad)
        {
            // Erster Blick der Verteilung auf das Video: es ist inzwischen weg.
            if (string.Equals(pfad, video, StringComparison.OrdinalIgnoreCase) && File.Exists(video))
                File.Delete(video);
            try { return File.GetAttributes(pfad); }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return null; }
        }

        var ergebnis = await Verteiler(LeseUndLoesche).DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);

        Assert.Equal(0, ergebnis.VideosMatched);
        Assert.Empty(Directory.GetFiles(Path.Combine(ausgabe, "23021-22369"), "*.link"));
        Assert.Contains(ergebnis.Messages, meldung => meldung.Contains("nicht mehr vorhanden", StringComparison.Ordinal));
    }

    private async Task<(string Videos, string Ausgabe, string Fallordner, string AltVideo)> ErsteVerteilungMitMpgAsync()
    {
        var videos = Path.Combine(_root, "Videos");
        Directory.CreateDirectory(videos);
        var altVideo = Path.Combine(videos, "H_23021-22369.mpg");
        File.WriteAllText(altVideo, "altes video");
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        await Verteiler().DistributeByHaltungAsync(Path.Combine(_root, "Sammel.pdf"), videos, ausgabe, CancellationToken.None);
        var fallordner = Path.Combine(ausgabe, "23021-22369");
        Assert.True(File.Exists(Path.Combine(fallordner, "H_23021-22369.mpg.link")));
        return (videos, ausgabe, fallordner, altVideo);
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

    // --- PR #85, Runde 6: ganze Kette, Rueckfall, Protokolle ---

    [Fact]
    public async Task Verweisziel_unter_verknuepftem_elternordner_wird_nicht_uebernommen()
    {
        var fallordner = Fallordner("23021-22369");
        var videoLink = Path.Combine(_root, "VideoLink");
        Directory.CreateDirectory(videoLink);
        var ziel = Path.Combine(videoLink, "H_23021-22369.mpg");
        File.WriteAllText(ziel, "video");
        var verweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.WriteAllText(verweis, ziel);
        var hinweise = new List<string>();

        var fall = Assert.Single(await DienstMitVerknuepfung(videoLink).ScanAsync(
            ScanWurzel, null, hinweise, CancellationToken.None));

        Assert.Equal("", fall.VideoPath);
        var hinweis = Assert.Single(hinweise);
        Assert.Contains(verweis, hinweis, StringComparison.Ordinal);
        Assert.Contains(ziel, hinweis, StringComparison.Ordinal);
    }

    [JunctionFact]
    public async Task Verweisziel_unter_echter_verzeichnis_verknuepfung_wird_nicht_uebernommen()
    {
        var fallordner = Fallordner("23021-22369");
        var echt = Path.Combine(_root, "VideoEcht");
        Directory.CreateDirectory(echt);
        File.WriteAllText(Path.Combine(echt, "H_23021-22369.mpg"), "video");
        var videoLink = Path.Combine(_root, "VideoLink");
        JunctionTestSupport.CreateDirectoryLink(videoLink, echt);
        var verweis = Path.Combine(fallordner, "H_23021-22369.mpg.link");
        File.WriteAllText(verweis, Path.Combine(videoLink, "H_23021-22369.mpg"));
        var hinweise = new List<string>();

        try
        {
            var fall = Assert.Single(await new TrainingCenterImportService().ScanAsync(
                ScanWurzel, null, hinweise, CancellationToken.None));

            Assert.Equal("", fall.VideoPath);
            Assert.Contains(verweis, Assert.Single(hinweise), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(videoLink);
        }
    }

    [JunctionFact]
    public async Task Scan_betritt_keinen_verknuepften_fallordner_und_nennt_ihn()
    {
        // Beleg fuer die Kette Direktvideo -> Scan-Wurzel: Unterordner betritt die Ordnersuche nur ohne
        // Verknuepfung; ein verknuepfter Fallordner liefert keinen Fall und steht in der Ordnerliste.
        Fallordner("23021-22369");
        var fremd = Path.Combine(_root, "Fremd");
        Directory.CreateDirectory(fremd);
        File.WriteAllText(Path.Combine(fremd, "H_99999-88888.mpg"), "fremdes video");
        File.WriteAllText(Path.Combine(fremd, "99999-88888_protokoll.json"), "{}");
        var link = Path.Combine(ScanWurzel, "99999-88888");
        JunctionTestSupport.CreateDirectoryLink(link, fremd);
        var uebersprungen = new List<string>();

        try
        {
            var faelle = await new TrainingCenterImportService().ScanAsync(
                ScanWurzel, uebersprungen, null, CancellationToken.None);

            Assert.Equal(["23021-22369"], faelle.Select(fall => fall.CaseId));
            Assert.Equal([link], uebersprungen);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public async Task Kein_verwendbares_direktvideo_faellt_auf_den_gueltigen_verweis_zurueck()
    {
        // Zwei Direktvideos, beide ausgeschlossen (Grafik, Uebersicht) und ohne Haltungsschluessel:
        // PickBestVideo waehlt keines.
        var fallordner = Fallordner("23021-22369");
        File.WriteAllText(Path.Combine(fallordner, "Grafik_g.mpg"), "grafikvideo");
        File.WriteAllText(Path.Combine(fallordner, "Uebersicht.mpg"), "uebersicht");
        var original = Path.Combine(_root, "H_23021-22369.mpg");
        File.WriteAllText(original, "original");
        File.WriteAllText(Path.Combine(fallordner, "H_23021-22369.mpg.link"), original);

        var fall = Assert.Single(await new TrainingCenterImportService().ScanAsync(ScanWurzel));

        Assert.Equal(original, fall.VideoPath);
    }

    // Review PR #85: Auch ein EINZELNES ausgeschlossenes Direktvideo (Grafikvideo *_g.mpg) ist kein
    // Inspektionsvideo; vorher nahm PickBestVideo es wegen des Einzelfall-Ruecksprungs trotzdem.
    [Fact]
    public async Task Einzelnes_grafikvideo_wird_nicht_genommen_der_verweis_gilt()
    {
        var fallordner = Fallordner("23021-22369");
        File.WriteAllText(Path.Combine(fallordner, "H_23021-22369_g.mpg"), "grafikvideo");
        var original = Path.Combine(_root, "H_23021-22369.mpg");
        File.WriteAllText(original, "original");
        File.WriteAllText(Path.Combine(fallordner, "H_23021-22369.mpg.link"), original);

        var fall = Assert.Single(await new TrainingCenterImportService().ScanAsync(ScanWurzel));

        Assert.Equal(original, fall.VideoPath);
    }

    [Fact]
    public async Task Verknuepftes_protokoll_wird_nicht_gelesen_sondern_gemeldet()
    {
        var fallordner = Fallordner("23021-22369");
        var protokoll = Path.Combine(fallordner, "23021-22369_protokoll.json");
        var hinweise = new List<string>();

        var faelle = await DienstMitVerknuepfung(protokoll).ScanAsync(ScanWurzel, null, hinweise, CancellationToken.None);

        Assert.Empty(faelle);
        var hinweis = Assert.Single(hinweise);
        Assert.Contains($"Protokoll «{protokoll}» ist eine Verknüpfung", hinweis, StringComparison.Ordinal);
    }

    private static Func<string, FileAttributes?> VerknuepfungFuer(string pfad)
        => p => string.Equals(p, pfad, StringComparison.OrdinalIgnoreCase)
            ? (Directory.Exists(p) ? FileAttributes.Directory : FileAttributes.Archive) | FileAttributes.ReparsePoint
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

    private static TrainingCenterImportService Verteiler(
        Func<string, FileAttributes?>? leseAttribute = null,
        Func<string, IEnumerable<string>>? dateienImOrdner = null)
        => new(
            pdfSeitenLesen: _ => new PdfTextExtraction(
                [string.Join("\n",
                [
                    "Kanalfernsehprotokoll / Inspektion: 1",
                    "Haltungsname:                Datum :                Wetter :               Operator :",
                    " 23021-22369                22.04.2014          schoen_trocken           Manuel Joschko"
                ])],
                ""),
            dateienImOrdner: dateienImOrdner,
            nachHaltungsordner: null,
            leseAttribute: leseAttribute);
}
