using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ShellProjectOpenPhotoReferenceTests
{
    [Fact]
    public void TryOpenProject_PersistiertAutomatischReparierteFotoReferenzen()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);

        var stalePhoto = Path.Combine(temp.Path, "Importdateien", "XTF", "Foto", "H_06-001_002.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(stalePhoto)!);
        File.WriteAllText(stalePhoto, "archiv");

        var project = BuildProjectWithStalePhoto(stalePhoto);
        var repo = new JsonProjectRepository();
        var save = repo.Save(project, projectFile);
        Assert.True(save.Ok, save.ErrorMessage);

        var centralPhotoDir = Path.Combine(temp.Path, "Fotos", "Haltungen", "06-001");
        Directory.CreateDirectory(centralPhotoDir);
        File.WriteAllText(Path.Combine(centralPhotoDir, "H_06-001_002.jpg"), "zentral");

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));

        var opened = shell.TryOpenProject(projectFile);

        Assert.True(opened);
        Assert.False(shell.Project.Dirty);

        var persisted = JsonSerializer.Deserialize<Project>(File.ReadAllText(projectFile));
        var persistedRecord = Assert.Single(persisted!.Data);
        var persistedFinding = Assert.Single(persistedRecord.VsaFindings);
        var persistedEntry = Assert.Single(persistedRecord.Protocol!.Current.Entries);

        Assert.Equal("Fotos/Haltungen/06-001/H_06-001_002.jpg", persistedFinding.FotoPath?.Replace('\\', '/'));
        Assert.Equal("Fotos/Haltungen/06-001/H_06-001_002.jpg", Assert.Single(persistedEntry.FotoPaths).Replace('\\', '/'));
    }

    private static Project BuildProjectWithStalePhoto(string stalePhoto)
    {
        var project = new Project { Name = "Foto-Test" };
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "06-001", FieldSource.Xtf, userEdited: false);
        record.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BAA", FotoPath = stalePhoto });
        record.Protocol = new ProtocolDocument();
        record.Protocol.Current.Entries.Add(new ProtocolEntry
        {
            Code = "BAA",
            FotoPaths = { stalePhoto }
        });
        project.Data.Add(record);
        return project;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "shell-open-photo-" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }
}
