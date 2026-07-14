using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class ProjectFileDiscoveryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ProjectFileDiscoveryServiceTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Instanzdienst_findet_alte_und_neue_Projektablage_ohne_Duplikate()
    {
        Directory.CreateDirectory(_root);
        var oldDirectory = Directory.CreateDirectory(Path.Combine(_root, "Alt")).FullName;
        var oldProject = Path.Combine(oldDirectory, "Altprojekt.json");
        File.WriteAllText(oldProject, "{}");

        var newDirectory = Directory.CreateDirectory(
            Path.Combine(_root, "Neu", "Projektdateien")).FullName;
        var newProject = Path.Combine(newDirectory, "projekt.json");
        File.WriteAllText(newProject, "{}");
        IProjectFileDiscovery discovery = new ProjectFileDiscoveryService();

        var files = discovery.FindProjectFiles([_root, _root.ToUpperInvariant()]);

        Assert.Equal(2, files.Count);
        Assert.Contains(oldProject, files, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(newProject, files, StringComparer.OrdinalIgnoreCase);
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
}
