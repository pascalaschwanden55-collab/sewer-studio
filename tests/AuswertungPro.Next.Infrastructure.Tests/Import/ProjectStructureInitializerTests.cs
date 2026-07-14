using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.WinCan;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ProjectStructureInitializerTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"ProjectStructureInitializerTests_{Guid.NewGuid():N}");

    [Fact]
    public async Task InstanceService_CreatesCompleteStructureForParallelCalls()
    {
        IProjectStructureInitializer initializer = new ProjectStructureInitializer();

        await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() =>
                    initializer.EnsureCreated(_tempDirectory))));

        Assert.True(Directory.Exists(Path.Combine(
            _tempDirectory,
            ProjectStructure.Importdateien,
            ProjectStructure.Datenbanken)));
        Assert.True(Directory.Exists(Path.Combine(
            _tempDirectory,
            ProjectStructure.Fotos,
            ProjectStructure.FotosSchaechte)));
        Assert.True(Directory.Exists(Path.Combine(
            _tempDirectory,
            ProjectStructure.RestorePoints)));
    }

    [Fact]
    public void ImportOrchestrator_UsesInjectedStructureInitializer()
    {
        var sourceDirectory = Path.Combine(_tempDirectory, "source");
        var projectDirectory = Path.Combine(_tempDirectory, "project");
        Directory.CreateDirectory(sourceDirectory);
        var initializer = new RecordingProjectStructureInitializer();
        var orchestrator = new ProjectImportOrchestrator(
            new XtfImportServiceAdapter(),
            new WinCanDbImportService(),
            projectStructure: initializer);

        _ = orchestrator.Import(
            sourceDirectory,
            projectDirectory,
            new Project());

        Assert.Equal(1, initializer.Calls);
        Assert.Equal(projectDirectory, initializer.LastProjectFolder);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen ist best effort.
        }
    }

    private sealed class RecordingProjectStructureInitializer : IProjectStructureInitializer
    {
        public int Calls { get; private set; }
        public string? LastProjectFolder { get; private set; }

        public void EnsureCreated(string projectFolder)
        {
            Calls++;
            LastProjectFolder = projectFolder;
        }
    }
}
