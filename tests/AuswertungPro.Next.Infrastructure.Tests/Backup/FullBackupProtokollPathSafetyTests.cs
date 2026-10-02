using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class FullBackupProtokollPathSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "sewerstudio-protokoll-schutz-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _directoryLinks = new();
    private readonly List<string> _fileLinks = new();

    [JunctionFact]
    public Task Zielordner_Verknuepfung_veraendert_vorhandenes_fremdes_Protokoll_nicht()
        => PruefeBlockiertesZielAsync(vorfahrVerknuepft: false, protokollVorhanden: true);

    [JunctionFact]
    public Task Zielordner_Verknuepfung_legt_kein_fremdes_Protokoll_an()
        => PruefeBlockiertesZielAsync(vorfahrVerknuepft: false, protokollVorhanden: false);

    [JunctionFact]
    public Task Vorfahr_Verknuepfung_veraendert_vorhandenes_fremdes_Protokoll_nicht()
        => PruefeBlockiertesZielAsync(vorfahrVerknuepft: true, protokollVorhanden: true);

    [JunctionFact]
    public Task Vorfahr_Verknuepfung_legt_kein_fremdes_Protokoll_an()
        => PruefeBlockiertesZielAsync(vorfahrVerknuepft: true, protokollVorhanden: false);

    [JunctionFact]
    public async Task Protokolldatei_Verknuepfung_bleibt_bei_Start_und_Fehlerprotokollierung_unveraendert()
    {
        var ziel = Folder("ziel");
        var fremdesProtokoll = Path.Combine(Folder("fremd"), "original.txt");
        byte[] original = [0, 1, 2, 0xFE, 0xFF, 10, 13];
        File.WriteAllBytes(fremdesProtokoll, original);
        var link = Path.Combine(ziel, FullBackupService.ProtokollDateiName);
        File.CreateSymbolicLink(link, fremdesProtokoll);
        _fileLinks.Add(link);
        const string quellfehler = "Kontrollierter Fehler beim Lesen der Quellen.";
        var service = new FullBackupService(() => throw new IOException(quellfehler));

        var result = await service.RunAsync(ziel);

        Assert.False(result.Success);
        Assert.Equal(quellfehler, result.Error);
        Assert.Equal(original, File.ReadAllBytes(fremdesProtokoll));
        Assert.False(Directory.Exists(Path.Combine(ziel, BackupPlanBuilder.TargetFolderName)));
    }

    private async Task PruefeBlockiertesZielAsync(bool vorfahrVerknuepft, bool protokollVorhanden)
    {
        var fremd = Folder("fremd");
        var link = Path.Combine(_root, "ziel-link");
        var ziel = vorfahrVerknuepft ? Path.Combine(link, "unterordner") : link;
        var physischesZiel = vorfahrVerknuepft ? Path.Combine(fremd, "unterordner") : fremd;
        Directory.CreateDirectory(physischesZiel);
        var fremdesProtokoll = Path.Combine(physischesZiel, FullBackupService.ProtokollDateiName);
        byte[] original = [0, 1, 2, 0xFE, 0xFF, 10, 13];
        if (protokollVorhanden)
            File.WriteAllBytes(fremdesProtokoll, original);
        JunctionTestSupport.CreateDirectoryLink(link, fremd);
        _directoryLinks.Add(link);
        var service = new FullBackupService(CreateSources);

        var result = await service.RunAsync(ziel);

        Assert.False(result.Success);
        Assert.Contains("Verknüpfung", result.Error, StringComparison.Ordinal);
        Assert.Equal(0, result.FilesCopied);
        if (protokollVorhanden)
            Assert.Equal(original, File.ReadAllBytes(fremdesProtokoll));
        else
            Assert.False(File.Exists(fremdesProtokoll));
        Assert.False(Directory.Exists(Path.Combine(physischesZiel, BackupPlanBuilder.TargetFolderName)));
    }

    private FullBackupSources CreateSources()
    {
        var brain = Folder("brain");
        File.WriteAllText(Path.Combine(brain, "gold.json"), "{}");
        return new FullBackupSources(
            RepoRoot: null,
            KnowledgeRoot: brain,
            LocalSewerStudioDir: Path.Combine(_root, "local"),
            RoamingSewerStudioDir: Path.Combine(_root, "roaming-sewer"),
            RoamingAuswertungProDir: Path.Combine(_root, "roaming-auswertung"),
            DesktopDir: Path.Combine(_root, "desktop"),
            AppVersion: "protokoll-schutz-test",
            EnvironmentVariables: new Dictionary<string, string>());
    }

    private string Folder(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        // Ausschliesslich eigene Tempdaten. Links zuerst einzeln entfernen;
        // eine fehlgeschlagene Linkloeschung verhindert den rekursiven Rest.
        var root = Path.GetFullPath(_root);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        Assert.StartsWith(temp, root, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("sewerstudio-protokoll-schutz-", Path.GetFileName(root), StringComparison.Ordinal);
        foreach (var link in _fileLinks)
        {
            Assert.StartsWith(root + Path.DirectorySeparatorChar, Path.GetFullPath(link), StringComparison.OrdinalIgnoreCase);
            var target = File.ResolveLinkTarget(link, returnFinalTarget: true);
            Assert.NotNull(target);
            Assert.StartsWith(root + Path.DirectorySeparatorChar, target.FullName, StringComparison.OrdinalIgnoreCase);
            File.Delete(link);
        }
        foreach (var link in _directoryLinks.OrderByDescending(path => path.Length))
        {
            Assert.StartsWith(root + Path.DirectorySeparatorChar, Path.GetFullPath(link), StringComparison.OrdinalIgnoreCase);
            var target = Directory.ResolveLinkTarget(link, returnFinalTarget: true);
            Assert.NotNull(target);
            Assert.StartsWith(root + Path.DirectorySeparatorChar, target.FullName, StringComparison.OrdinalIgnoreCase);
            Directory.Delete(link, recursive: false);
        }
        if (Directory.Exists(root))
        {
            BackupTargetPathGuard.EnsureTreeIsSafe(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
