using System.Text.Json;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class BackupProjectIdentityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewerstudio-backup-identity-" + Guid.NewGuid());

    [Fact]
    public async Task AlteManifestZuordnung_BehaeltOfflineKopie_NachNeuerKuerzererQuelleUndMehrerenRotationen()
    {
        var oldSource = Path.Combine(_root, "zz_altes_langes_projekt");
        var newSource = Path.Combine(_root, "a");
        var oldTarget = Path.Combine("Projekte", "01_zz_altes_langes_projekt");
        var sources = CreateSources(newSource, oldSource);
        Write(Path.Combine(newSource, "neu.txt"), "neue Quelle");
        await SeedLegacyBackupAsync((oldSource, oldTarget));
        var service = new FullBackupService(() => sources, availableBytes: _ => long.MaxValue);

        // Jede Folgeaenderung erzeugt wirklich einen neuen Versionsstand. Der Test
        // prueft erst danach, damit eine voruebergehende Versionskopie nicht genuegt.
        for (var run = 0; run < BackupVersionRetention.MaxStaende + 2; run++)
        {
            Write(Path.Combine(sources.KnowledgeRoot, "wechsel.txt"), "Stand " + run);
            var result = await service.RunAsync(TargetParent);
            Assert.True(result.Success, result.Error);
            Assert.Contains(result.SkippedFiles, warning => warning.Contains(oldSource, StringComparison.Ordinal));
        }

        var stands = Directory.EnumerateDirectories(Path.Combine(BackupRoot, BackupVersionRetention.VersionsFolderName))
            .Count(path => BackupVersionRetention.IsStandName(Path.GetFileName(path)));
        Assert.Equal(BackupVersionRetention.MaxStaende, stands);
        Assert.True(File.Exists(Path.Combine(BackupRoot, oldTarget, "unersetzlich.txt")),
            "Die einzige Kopie der fehlenden Quelle muss im bisherigen Spiegel erhalten bleiben.");
        Assert.Equal("alter Bestand", File.ReadAllText(Path.Combine(BackupRoot, oldTarget, "unersetzlich.txt")));
        Assert.Equal(oldTarget, ReadProjectTargets()[oldSource]);
        Assert.True((await BackupManifestIntegrity.VerifyAsync(BackupRoot)).IsValid);
    }

    [Fact]
    public async Task NeueElternquelle_BehaeltKonfigurierteFehlendeAltquelle_NachMehrerenRotationen()
    {
        var parentSource = Path.Combine(_root, "projekte");
        var oldSource = Path.Combine(parentSource, "offline");
        var oldTarget = Path.Combine("Projekte", "01_offline");
        var sources = CreateSources(parentSource, oldSource);
        Write(Path.Combine(parentSource, "neues_projekt.txt"), "neu");
        await SeedLegacyBackupAsync((oldSource, oldTarget));
        var service = new FullBackupService(() => sources, availableBytes: _ => long.MaxValue);

        for (var run = 0; run < BackupVersionRetention.MaxStaende + 2; run++)
        {
            Write(Path.Combine(sources.KnowledgeRoot, "wechsel.txt"), "Stand " + run);
            var result = await service.RunAsync(TargetParent);
            Assert.True(result.Success, result.Error);
        }

        Assert.True(File.Exists(Path.Combine(BackupRoot, oldTarget, "unersetzlich.txt")),
            "Eine neue Elternquelle darf die konfigurierte, fehlende Altquelle nicht aus dem Schutz entfernen.");
        Assert.Equal("alter Bestand", File.ReadAllText(Path.Combine(BackupRoot, oldTarget, "unersetzlich.txt")));
        Assert.Equal(oldTarget, ReadProjectTargets()[oldSource]);
    }

    [Fact]
    public async Task NeueQuelleMitGleichemOrdnernamen_DarfAltesZielNichtUebernehmen()
    {
        var oldSource = Path.Combine(_root, "alter_langer_elternordner", "projekt");
        var newSource = Path.Combine(_root, "a", "projekt");
        var oldTarget = Path.Combine("Projekte", "01_projekt");
        var sources = CreateSources(newSource, oldSource);
        Write(Path.Combine(newSource, "unersetzlich.txt"), "neuer anderer Bestand");
        await SeedLegacyBackupAsync((oldSource, oldTarget));

        var result = await new FullBackupService(() => sources, availableBytes: _ => long.MaxValue).RunAsync(TargetParent);

        Assert.True(result.Success, result.Error);
        var targets = ReadProjectTargets();
        Assert.Equal(oldTarget, targets[oldSource]);
        Assert.NotEqual(targets[oldSource], targets[newSource]);
        Assert.Equal("alter Bestand", File.ReadAllText(Path.Combine(BackupRoot, oldTarget, "unersetzlich.txt")));
        Assert.Equal("neuer anderer Bestand", File.ReadAllText(Path.Combine(BackupRoot, targets[newSource], "unersetzlich.txt")));
    }

    [Theory]
    [InlineData("kaputtes-json")]
    [InlineData("fehlender-plan")]
    [InlineData("fehlendes-manifest")]
    [InlineData("eine-quelle-zwei-ziele")]
    [InlineData("zwei-quellen-ein-ziel")]
    [InlineData("ziel-ausserhalb-projekte")]
    public async Task UnklareAlteZuordnung_StopptOhneSpiegelOderVersionenZuVeraendern(string damage)
    {
        var oldSource = Path.Combine(_root, "altes_projekt");
        var oldTarget = Path.Combine("Projekte", "01_altes_projekt");
        var sources = CreateSources(null, oldSource);
        await SeedLegacyBackupAsync((oldSource, oldTarget));
        var manifestPath = Path.Combine(BackupRoot, "manifest.json");
        switch (damage)
        {
            case "kaputtes-json": File.WriteAllText(manifestPath, "{"); break;
            case "fehlender-plan": File.WriteAllText(manifestPath, "{}"); break;
            case "fehlendes-manifest": File.Delete(manifestPath); break;
            case "eine-quelle-zwei-ziele":
                await WriteLegacyManifestAsync((oldSource, oldTarget), (oldSource, Path.Combine("Projekte", "02_anderes")));
                break;
            case "zwei-quellen-ein-ziel":
                await WriteLegacyManifestAsync((oldSource, oldTarget), (Path.Combine(_root, "fremd"), oldTarget));
                break;
            case "ziel-ausserhalb-projekte":
                await WriteLegacyManifestAsync((oldSource, "KI_BRAIN"));
                break;
        }
        var before = SnapshotBackup();

        var result = await new FullBackupService(() => sources, availableBytes: _ => long.MaxValue).RunAsync(TargetParent);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(before, SnapshotBackup());
    }

    [Fact]
    public async Task AbgebrochenerErstlauf_MitLeerenProjektordnern_KannAmGleichenZielWiederholtWerden()
    {
        var source = Path.Combine(_root, "projekt");
        var sources = CreateSources(source, source);
        File.Delete(Path.Combine(sources.KnowledgeRoot, "wechsel.txt"));
        Write(Path.Combine(source, "unterordner", "unersetzlich.txt"), "Original bleibt");
        var service = new FullBackupService(() => sources, availableBytes: _ => long.MaxValue);
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<FullBackupProgress>(value =>
        {
            if (value.Component == "Projekte" && value.FilesDone > 0)
                cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunAsync(TargetParent, progress, cancellation.Token));

        var projectsRoot = Path.Combine(BackupRoot, "Projekte");
        Assert.True(Directory.Exists(projectsRoot));
        Assert.Empty(Directory.EnumerateFiles(projectsRoot, "*", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.Combine(BackupRoot, "manifest.json")));
        var repeated = await service.RunAsync(TargetParent);

        Assert.True(repeated.Success, repeated.Error);
        var target = ReadProjectTargets()[source];
        Assert.Equal("Original bleibt", File.ReadAllText(Path.Combine(BackupRoot, target, "unterordner", "unersetzlich.txt")));
        Assert.Equal("Original bleibt", File.ReadAllText(Path.Combine(source, "unterordner", "unersetzlich.txt")));
    }

    [Fact]
    public async Task EntfernteQuelle_MitGeschuetztemVideo_BehaeltZuordnungOhneWeitereQuelldateienZuKopieren()
    {
        var source = Path.Combine(_root, "altes_projekt");
        var sources = CreateSources(null, source);
        Write(Path.Combine(source, "haltung.mp4"), "gesichertes Video");
        Write(Path.Combine(source, "alter_bericht.txt"), "alter Bericht");
        var service = new FullBackupService(() => sources, availableBytes: _ => long.MaxValue);
        var first = await service.RunAsync(TargetParent);
        Assert.True(first.Success, first.Error);
        var oldTarget = ReadProjectTargets()[source];

        sources = sources with { OptionalProjectRoots = [], IncludeProjectVideos = false };
        Write(Path.Combine(source, "haltung.mp4"), "nachtraeglich geaendertes Original");
        Write(Path.Combine(source, "neuer_bericht.txt"), "nicht mehr zur Sicherung ausgewaehlt");
        for (var run = 0; run < 2; run++)
        {
            var result = await service.RunAsync(TargetParent);
            Assert.True(result.Success, result.Error);
        }

        Assert.Equal("gesichertes Video", File.ReadAllText(Path.Combine(BackupRoot, oldTarget, "haltung.mp4")));
        Assert.False(File.Exists(Path.Combine(BackupRoot, oldTarget, "alter_bericht.txt")));
        Assert.False(File.Exists(Path.Combine(BackupRoot, oldTarget, "neuer_bericht.txt")));
        Assert.Equal(oldTarget, ReadProjectTargets()[source]);
        Assert.True((await BackupManifestIntegrity.VerifyAsync(BackupRoot)).IsValid);
    }

    private string TargetParent => Path.Combine(_root, "ziel");
    private string BackupRoot => Path.Combine(TargetParent, BackupPlanBuilder.TargetFolderName);

    private FullBackupSources CreateSources(string? current, string previous)
    {
        var brain = Path.Combine(_root, "brain");
        Write(Path.Combine(brain, "wechsel.txt"), "Start");
        return new FullBackupSources(null, brain, Path.Combine(_root, "local"),
            Path.Combine(_root, "roaming"), Path.Combine(_root, "legacy"), Path.Combine(_root, "desktop"),
            "identity-test", new Dictionary<string, string>(),
            ProjectRoots: current is null ? [] : [current], IncludeProjectVideos: true,
            OptionalProjectRoots: [previous]);
    }

    private async Task SeedLegacyBackupAsync(params (string Source, string Target)[] mappings)
    {
        Assert.Null(BackupTargetGuard.MarkerGuard.ValidateAndCreateMarker(BackupRoot));
        foreach (var mapping in mappings)
            Write(Path.Combine(BackupRoot, mapping.Target, "unersetzlich.txt"), "alter Bestand");
        await WriteLegacyManifestAsync(mappings);
    }

    private async Task WriteLegacyManifestAsync(params (string Source, string Target)[] mappings)
    {
        // Der alte Vertrag hat keine separate Quell-ID: die Zuordnung steht in Plan.
        var manifest = new
        {
            Plan = new[] { new { Name = "Projekte", Sources = mappings.Select(item =>
                new { SourceRoot = item.Source, TargetRelativeRoot = item.Target }).ToArray() } },
            Files = await BackupManifestIntegrity.CreateEntriesAsync(BackupRoot)
        };
        File.WriteAllText(Path.Combine(BackupRoot, "manifest.json"), JsonSerializer.Serialize(manifest));
    }

    private Dictionary<string, string> ReadProjectTargets()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(BackupRoot, "manifest.json")));
        return manifest.RootElement.GetProperty("Plan").EnumerateArray()
            .Single(component => component.GetProperty("Name").GetString() == "Projekte")
            .GetProperty("Sources").EnumerateArray()
            .ToDictionary(source => source.GetProperty("SourceRoot").GetString()!,
                source => source.GetProperty("TargetRelativeRoot").GetString()!, StringComparer.OrdinalIgnoreCase);
    }

    private string[] SnapshotBackup() => Directory.EnumerateFiles(BackupRoot, "*", SearchOption.AllDirectories)
        .Where(path => Path.GetFileName(path) != ".sicherung.lock")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(path => Path.GetRelativePath(BackupRoot, path) + ":" + Convert.ToHexString(File.ReadAllBytes(path)))
        .ToArray();

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
