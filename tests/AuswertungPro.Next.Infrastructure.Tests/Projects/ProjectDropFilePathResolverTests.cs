using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class ProjectDropFilePathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ProjectDropFilePathResolverTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolveProjectFile_bevorzugt_verbindliche_Projektdatei_vor_weiteren_Json_Dateien()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Projektdateien"));
        var expected = Path.Combine(_root, "Projektdateien", "projekt.json");
        File.WriteAllText(expected, "{}");
        File.WriteAllText(Path.Combine(_root, "anderes.json"), "{}");
        IProjectDropPathResolver resolver = new ProjectDropFilePathResolver();

        var result = resolver.ResolveProjectFile(_root);

        Assert.Equal(expected, result);
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
