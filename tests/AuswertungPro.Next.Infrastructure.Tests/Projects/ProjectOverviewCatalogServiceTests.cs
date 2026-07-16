using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class ProjectOverviewCatalogServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ProjectOverviewCatalogServiceTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_liest_entfernt_Duplikate_und_sortiert_letztes_Projekt_zuerst()
    {
        var last = WriteProject(
            "Letztes.json",
            "Letztes Projekt",
            "2024-01-01T08:00:00Z",
            holdingCount: 2,
            schachtCount: 1);
        var newer = WriteProject(
            "Neuer.json",
            "Neueres Projekt",
            "2025-01-01T08:00:00Z",
            holdingCount: 1,
            schachtCount: 0);
        var discovery = new RecordingDiscovery(last, newer);
        var service = new ProjectOverviewCatalogService(discovery);

        var result = service.Load(new ProjectOverviewCatalogRequest(
            last,
            [newer, last],
            [],
            [_root]));

        Assert.Equal(2, result.Count);
        Assert.Equal("Letztes Projekt", result[0].Name);
        Assert.True(result[0].IsLastProject);
        Assert.Equal(2, result[0].HoldingCount);
        Assert.Equal(1, result[0].SchachtCount);
        Assert.Equal("Neueres Projekt", result[1].Name);
        Assert.Equal([_root], discovery.LastScanRoots);
    }

    [Fact]
    public void Load_blendet_markierte_Dateien_aus()
    {
        var visible = WriteProject("Sichtbar.json", "Sichtbar", "2025-01-01T08:00:00Z", 0, 0);
        var hidden = WriteProject("Versteckt.json", "Versteckt", "2025-01-02T08:00:00Z", 0, 0);
        var service = new ProjectOverviewCatalogService(new RecordingDiscovery(visible, hidden));

        var result = service.Load(new ProjectOverviewCatalogRequest(
            null,
            [],
            [hidden],
            [_root]));

        var entry = Assert.Single(result);
        Assert.Equal(visible, entry.Path);
    }

    [Fact]
    public void Load_meldet_defekte_Datei_und_laesst_gueltige_Dateien_stehen()
    {
        var projectDirectory = Directory.CreateDirectory(
            Path.Combine(_root, "Defektes Projekt", "Projektdateien")).FullName;
        var corrupt = Path.Combine(projectDirectory, "projekt.json");
        File.WriteAllText(corrupt, "kein json");
        var valid = WriteProject("Gueltig.json", "Gueltig", "2025-01-01T08:00:00Z", 1, 0);
        var service = new ProjectOverviewCatalogService(new RecordingDiscovery(corrupt, valid));

        var result = service.Load(new ProjectOverviewCatalogRequest(
            null,
            [],
            [],
            [_root]));

        Assert.Equal(2, result.Count);
        var corruptEntry = Assert.Single(result, entry => entry.IsCorrupt);
        Assert.Equal("Defektes Projekt", corruptEntry.Name);
        Assert.Equal("Projektdatei konnte nicht gelesen werden.", corruptEntry.Description);
        Assert.Contains(result, entry => entry.Name == "Gueltig" && !entry.IsCorrupt);
    }

    private string WriteProject(
        string fileName,
        string name,
        string modifiedAtUtc,
        int holdingCount,
        int schachtCount)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, fileName);
        var holdings = string.Join(',', Enumerable.Repeat("{}", holdingCount));
        var schaechte = string.Join(',', Enumerable.Repeat("{}", schachtCount));
        File.WriteAllText(
            path,
            $"{{\"Name\":\"{name}\",\"Description\":\"Test\",\"ModifiedAtUtc\":\"{modifiedAtUtc}\",\"Data\":[{holdings}],\"SchaechteData\":[{schaechte}]}}");
        return path;
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
            // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
        }
    }

    private sealed class RecordingDiscovery(params string[] projectPaths) : IProjectFileDiscovery
    {
        public IReadOnlyList<string> LastScanRoots { get; private set; } = [];

        public IReadOnlyList<string> FindProjectFiles(IEnumerable<string> baseDirectories)
        {
            LastScanRoots = baseDirectories.ToList();
            return projectPaths;
        }
    }
}
