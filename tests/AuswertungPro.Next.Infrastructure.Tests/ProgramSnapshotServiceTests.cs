using System.IO.Compression;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProgramSnapshotFileCatalogTests
{
    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("artifacts")]
    [InlineData(".tmp")]
    [InlineData("sidecar\\.venv")]
    [InlineData("basemap_tiles")]
    [InlineData(".worktrees")]
    [InlineData("src\\Projekt\\bin")]
    public void Ableitbare_Ordner_bleiben_draussen(string relativer)
        => Assert.True(ProgramSnapshotFileCatalog.IsExcludedDirectory(relativer));

    [Theory]
    [InlineData("src")]
    [InlineData(".git")]
    [InlineData("sidecar\\models")]
    [InlineData("tests")]
    [InlineData("training\\scripts")]
    public void Unersetzliche_Ordner_bleiben_drin(string relativer)
        => Assert.False(ProgramSnapshotFileCatalog.IsExcludedDirectory(relativer));

    [Theory]
    [InlineData("src")]
    [InlineData("src\\AuswertungPro.Next.UI")]
    [InlineData(".git")]
    [InlineData("sidecar")]
    [InlineData("sidecar\\models")]
    [InlineData("tests")]
    [InlineData("tools")]
    [InlineData("")]
    [InlineData(".")]
    public void Pflichtordner_werden_erkannt(string relativer)
        => Assert.True(ProgramSnapshotFileCatalog.IsRequiredDirectory(relativer));

    [Theory]
    [InlineData("docs")]
    [InlineData("training")]
    [InlineData("integrations\\qgis")]
    // Nur die oberste Ebene zaehlt: ein fremder Unterordner mit gleichem Namen
    // darf nicht die ganze Sicherung sperren.
    [InlineData("docs\\src")]
    [InlineData("integrations\\tests")]
    public void Nebensaechliche_Ordner_sind_keine_Pflicht(string relativer)
        => Assert.False(ProgramSnapshotFileCatalog.IsRequiredDirectory(relativer));

    [Fact]
    public void Eine_Datei_unter_einem_ausgeschlossenen_Ordner_ist_ausgeschlossen()
    {
        Assert.True(ProgramSnapshotFileCatalog.IsExcludedPath("basemap_tiles\\12\\34\\kachel.png"));
        Assert.True(ProgramSnapshotFileCatalog.IsExcludedPath("src\\Projekt\\obj\\Debug\\a.dll"));
        Assert.False(ProgramSnapshotFileCatalog.IsExcludedPath("src\\Projekt\\Datei.cs"));
    }

    [Fact]
    public void Eine_Datei_mit_ausgeschlossenem_Namen_bleibt_drin()
    {
        // Nur Ordner werden ausgeschlossen. Eine Datei namens "artifacts.json"
        // oder "bin" darf nicht mit einem Ordner verwechselt werden.
        Assert.False(ProgramSnapshotFileCatalog.IsExcludedPath("src\\artifacts.json"));
        Assert.False(ProgramSnapshotFileCatalog.IsExcludedPath("bin"));
    }
}

public sealed class ProgramSnapshotServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewer-programm-snapshot-" + Guid.NewGuid().ToString("N"));

    private string ProgramRoot => Path.Combine(_root, "Programm");

    private string ZipPath => Path.Combine(_root, "Ziel", "programm.zip");

    public ProgramSnapshotServiceTests()
    {
        Write("src\\Datei.cs", "echter Quellcode");
        Write(".git\\HEAD", "ref: refs/heads/master");
        Write("sidecar\\models\\gewicht.pt", "modell");
        Write("artifacts\\build\\gebaut.dll", "ableitbar");
        Write("src\\Projekt\\obj\\zwischen.tmp", "ableitbar");
        Write("basemap_tiles\\12\\kachel.png", "ableitbar");
        Write(".worktrees\\a\\Datei.cs", "ableitbar");
        Directory.CreateDirectory(Path.GetDirectoryName(ZipPath)!);
    }

    [Fact]
    public async Task Packt_nur_das_Unersetzliche()
    {
        var result = await CreateAsync();

        Assert.True(result.Success, result.Error);
        var entries = ReadEntryNames();
        Assert.Contains("src/Datei.cs", entries);
        Assert.Contains(".git/HEAD", entries);
        Assert.Contains("sidecar/models/gewicht.pt", entries);
        Assert.DoesNotContain("artifacts/build/gebaut.dll", entries);
        Assert.DoesNotContain("src/Projekt/obj/zwischen.tmp", entries);
        Assert.DoesNotContain("basemap_tiles/12/kachel.png", entries);
        Assert.DoesNotContain(".worktrees/a/Datei.cs", entries);
        Assert.Equal(3, result.FileCount);
    }

    [Theory]
    [InlineData("sidecar\\models\\gewicht.pt")]
    [InlineData(".git\\objects\\pack\\a.pack")]
    [InlineData("bilder\\foto.JPG")]
    [InlineData("archiv.zip")]
    public void Bereits_komprimierte_Dateien_werden_nicht_erneut_gepackt(string relativer)
        => Assert.Equal(
            CompressionLevel.NoCompression,
            ProgramSnapshotService.ChooseCompressionLevel(relativer));

    [Theory]
    [InlineData("src\\Datei.cs")]
    [InlineData("projekt.json")]
    [InlineData("readme.md")]
    public void Quellcode_wird_gepackt(string relativer)
        => Assert.Equal(
            CompressionLevel.Optimal,
            ProgramSnapshotService.ChooseCompressionLevel(relativer));

    [Fact]
    public async Task Die_Sicherung_wird_nie_groesser_als_ihr_Inhalt()
    {
        // Modellgewichte sind bereits komprimiert. Wuerden sie erneut gepackt,
        // waere die Sicherung groesser als das Original (real gemessen: 894 -> 939 MB).
        Write("sidecar\\models\\gross.pt", ZufallsBytesAlsText(400_000));

        var result = await CreateAsync();

        Assert.True(result.Success, result.Error);
        using var zip = ZipFile.OpenRead(ZipPath);
        var eintrag = zip.Entries.Single(e => e.FullName == "sidecar/models/gross.pt");
        Assert.Equal(eintrag.Length, eintrag.CompressedLength);
    }

    /// <summary>Schwer komprimierbarer Inhalt mit festem Startwert (reproduzierbar).</summary>
    private static string ZufallsBytesAlsText(int laenge)
    {
        var zufall = new Random(1234);
        var puffer = new char[laenge];
        for (var i = 0; i < laenge; i++)
            puffer[i] = (char)zufall.Next(33, 126);
        return new string(puffer);
    }

    [Fact]
    public async Task Schreibt_ein_Manifest_mit_Zahlen()
    {
        await CreateAsync();

        var entries = ReadEntryNames();
        Assert.Contains("_manifest.json", entries);

        using var zip = ZipFile.OpenRead(ZipPath);
        using var reader = new StreamReader(zip.GetEntry("_manifest.json")!.Open());
        var text = await reader.ReadToEndAsync();
        Assert.Contains("\"Kind\":\"ProgramSnapshot\"", text);
        Assert.Contains("\"FileCount\":3", text);
    }

    [Fact]
    public async Task Laesst_die_Quelle_unveraendert()
    {
        var vorher = Directory
            .EnumerateFiles(ProgramRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        await CreateAsync();

        var nachher = Directory
            .EnumerateFiles(ProgramRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(vorher, nachher);
    }

    [Fact]
    public async Task Ein_Ziel_im_Programmordner_wird_abgelehnt()
    {
        var service = new ProgramSnapshotService();
        var result = await service.CreateAsync(
            new ProgramSnapshotRequest(ProgramRoot, Path.Combine(ProgramRoot, "selbst.zip")));

        Assert.False(result.Success);
        Assert.Contains("nicht im Programmordner", result.Error);
        Assert.False(File.Exists(Path.Combine(ProgramRoot, "selbst.zip")));
    }

    [Fact]
    public async Task Eine_fehlende_Quelle_meldet_statt_zu_werfen()
    {
        var service = new ProgramSnapshotService();
        var result = await service.CreateAsync(
            new ProgramSnapshotRequest(Path.Combine(_root, "gibtesnicht"), ZipPath));

        Assert.False(result.Success);
        Assert.Contains("nicht gefunden", result.Error);
        Assert.False(File.Exists(ZipPath));
    }

    [Fact]
    public async Task Ein_zweiter_Lauf_ersetzt_die_Datei_vollstaendig()
    {
        await CreateAsync();
        var ersteGroesse = new FileInfo(ZipPath).Length;

        Write("src\\Zweite.cs", new string('x', 5000));
        var result = await CreateAsync();

        Assert.True(result.Success, result.Error);
        Assert.Equal(4, result.FileCount);
        Assert.Contains("src/Zweite.cs", ReadEntryNames());
        Assert.True(new FileInfo(ZipPath).Length > ersteGroesse);
        // Kein Temp-Rest neben dem Ziel.
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(ZipPath)!, ".*.tmp"));
    }

    [Fact]
    public async Task Ein_Abbruch_hinterlaesst_keine_halbe_Sicherung()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = new ProgramSnapshotService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateAsync(new ProgramSnapshotRequest(ProgramRoot, ZipPath), null, cts.Token));

        Assert.False(File.Exists(ZipPath));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(ZipPath)!, ".*.tmp"));
    }

    // ---- Gesamtaudit 2026-08-14, P1-2: eine unvollstaendige Sicherung darf nicht
    // ---- als erfolgreich gelten, und die fertige Datei wird nachgeprueft.

    [Fact]
    public async Task Ein_unlesbarer_Pflichtordner_bricht_die_Sicherung_ab()
    {
        var service = MitUnlesbaremOrdner(Path.Combine(ProgramRoot, "src"));

        var result = await service.CreateAsync(new ProgramSnapshotRequest(ProgramRoot, ZipPath));

        Assert.False(result.Success);
        Assert.Contains("Unersetzliche Ordner", result.Error);
        Assert.Contains("src", result.Error);
        Assert.Contains("src", result.UnreadableDirectoriesOrEmpty);
        // Vor allem: es entsteht keine Datei, die spaeter fuer vollstaendig gehalten wird.
        Assert.False(File.Exists(ZipPath));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(ZipPath)!, ".*.tmp"));
    }

    [Fact]
    public async Task Ein_unlesbarer_nebensaechlicher_Ordner_bleibt_erfolgreich_aber_sichtbar()
    {
        Write("docs\\anleitung.md", "text");
        var service = MitUnlesbaremOrdner(Path.Combine(ProgramRoot, "docs"));

        var result = await service.CreateAsync(new ProgramSnapshotRequest(ProgramRoot, ZipPath));

        Assert.True(result.Success, result.Error);
        Assert.Contains("docs", result.UnreadableDirectoriesOrEmpty);
        Assert.DoesNotContain("docs/anleitung.md", ReadEntryNames());

        // und die Luecke steht auch im Manifest, nicht nur im Ergebnisobjekt
        using var zip = ZipFile.OpenRead(ZipPath);
        using var reader = new StreamReader(zip.GetEntry("_manifest.json")!.Open());
        var text = await reader.ReadToEndAsync();
        Assert.Contains("UnreadableDirectories", text);
        Assert.Contains("docs", text);
    }

    [Fact]
    public async Task Die_Pruefsumme_liegt_neben_der_Sicherung_und_passt_zur_Datei()
    {
        var result = await CreateAsync();

        Assert.True(result.Success, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.ArchiveSha256));

        var nebendatei = ZipPath + ".sha256";
        Assert.True(File.Exists(nebendatei));
        Assert.Contains(result.ArchiveSha256!, File.ReadAllText(nebendatei));

        // gegen die echte Datei nachgerechnet
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(ZipPath);
        var erwartet = Convert.ToHexString(sha.ComputeHash(stream));
        Assert.Equal(erwartet, result.ArchiveSha256);
    }

    [Fact]
    public void Eine_beschaedigte_Sicherung_wird_bei_der_Nachpruefung_erkannt()
    {
        var archiv = Path.Combine(_root, "kaputt.zip");
        var inhalt = new string('a', 2000);
        using (var zip = ZipFile.Open(archiv, ZipArchiveMode.Create))
        {
            // unkomprimiert, damit der Inhalt im Rohbyte-Strom auffindbar ist
            var eintrag = zip.CreateEntry("src/Datei.cs", CompressionLevel.NoCompression);
            using (var stream = eintrag.Open())
                stream.Write(System.Text.Encoding.UTF8.GetBytes(inhalt));
            zip.CreateEntry("_manifest.json");
        }

        // Nutzdaten verdrehen: die gespeicherte CRC-Summe passt danach nicht mehr.
        var bytes = File.ReadAllBytes(archiv);
        var start = FindeNutzdaten(bytes);
        Assert.True(start >= 0, "Nutzdaten im Archiv nicht gefunden - Test waere sonst wirkungslos");
        for (var i = start; i < start + 100 && i < bytes.Length; i++)
            bytes[i] ^= 0xFF;
        File.WriteAllBytes(archiv, bytes);

        var fehler = ProgramSnapshotService.VerifyArchive(archiv, expectedFileCount: 1, CancellationToken.None);

        Assert.NotNull(fehler);
        Assert.Contains("Nachpruefung fehlgeschlagen", fehler);
    }

    [Fact]
    public void Eine_fehlende_Datei_faellt_bei_der_Nachpruefung_auf()
    {
        var archiv = Path.Combine(_root, "zuwenig.zip");
        using (var zip = ZipFile.Open(archiv, ZipArchiveMode.Create))
        {
            zip.CreateEntry("src/Datei.cs");
            zip.CreateEntry("_manifest.json");
        }

        var fehler = ProgramSnapshotService.VerifyArchive(archiv, expectedFileCount: 7, CancellationToken.None);

        Assert.NotNull(fehler);
        Assert.Contains("erwartet 7", fehler);
    }

    [Fact]
    public void Ein_fehlendes_Manifest_faellt_bei_der_Nachpruefung_auf()
    {
        var archiv = Path.Combine(_root, "ohnemanifest.zip");
        using (var zip = ZipFile.Open(archiv, ZipArchiveMode.Create))
            zip.CreateEntry("src/Datei.cs");

        var fehler = ProgramSnapshotService.VerifyArchive(archiv, expectedFileCount: 1, CancellationToken.None);

        Assert.NotNull(fehler);
        Assert.Contains("Manifest fehlt", fehler);
    }

    /// <summary>Sucht den Beginn einer laengeren Folge von 'a' (die Nutzdaten).</summary>
    private static int FindeNutzdaten(byte[] bytes)
    {
        const byte a = (byte)'a';
        var lauf = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == a)
            {
                lauf++;
                if (lauf == 64)
                    return i - 63;
            }
            else
            {
                lauf = 0;
            }
        }

        return -1;
    }

    /// <summary>
    /// Dienst, dessen Ordnerdurchlauf fuer genau einen Ordner wirft — so wie ein
    /// gesperrter oder rechtlich unzugaenglicher Ordner im Echtbetrieb.
    /// </summary>
    private static ProgramSnapshotService MitUnlesbaremOrdner(string gesperrterPfad)
        => new(
            null,
            pfad => Pruefe(pfad, gesperrterPfad, Directory.EnumerateDirectories),
            pfad => Pruefe(pfad, gesperrterPfad, Directory.EnumerateFiles));

    private static IEnumerable<string> Pruefe(
        string pfad,
        string gesperrterPfad,
        Func<string, IEnumerable<string>> echt)
    {
        if (string.Equals(
                Path.TrimEndingDirectorySeparator(pfad),
                Path.TrimEndingDirectorySeparator(gesperrterPfad),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Zugriff verweigert: {pfad}");
        }

        return echt(pfad);
    }

    private Task<ProgramSnapshotResult> CreateAsync()
        => new ProgramSnapshotService().CreateAsync(new ProgramSnapshotRequest(ProgramRoot, ZipPath));

    private string[] ReadEntryNames()
    {
        using var zip = ZipFile.OpenRead(ZipPath);
        return zip.Entries.Select(entry => entry.FullName).ToArray();
    }

    private void Write(string relativePath, string content)
    {
        var full = Path.Combine(ProgramRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Aufraeumen darf den Test nicht zum Scheitern bringen.
        }
    }
}
