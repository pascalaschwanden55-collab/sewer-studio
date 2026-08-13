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
