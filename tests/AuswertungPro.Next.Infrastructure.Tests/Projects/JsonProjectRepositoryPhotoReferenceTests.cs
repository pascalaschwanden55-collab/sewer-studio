using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Projects;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class JsonProjectRepositoryPhotoReferenceTests
{
    [Fact]
    public void Load_NormalizesStaleImportPhotoReferencesToCentralHoldingFolder()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);

        var stale = Path.Combine(temp.Path, "Importdateien", "XTF", "Foto", "H_06-001_002.jpg");
        var project = BuildProjectWithStalePhoto(stale);
        var repo = new JsonProjectRepository();
        var save = repo.Save(project, projectFile);
        Assert.True(save.Ok, save.ErrorMessage);

        var photoDir = Path.Combine(temp.Path, "Fotos", "Haltungen", "06-001");
        Directory.CreateDirectory(photoDir);
        File.WriteAllText(Path.Combine(photoDir, "H_06-001_002.jpg"), "bild");

        var load = repo.Load(projectFile);

        Assert.True(load.Ok, load.ErrorMessage);
        var record = Assert.Single(load.Value!.Data);
        Assert.Equal("Fotos/Haltungen/06-001/H_06-001_002.jpg", Assert.Single(record.Protocol!.Current.Entries[0].FotoPaths));
        Assert.Equal("Fotos/Haltungen/06-001/H_06-001_002.jpg", Assert.Single(record.VsaFindings).FotoPath);
        Assert.True(load.Value.Dirty, "Automatische Foto-Referenz-Reparatur muss beim naechsten Speichern persistiert werden.");
    }

    [Fact]
    public void Save_PersistsNormalizedCentralPhotoReferences()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);

        var photoDir = Path.Combine(temp.Path, "Fotos", "Haltungen", "06-001");
        Directory.CreateDirectory(photoDir);
        File.WriteAllText(Path.Combine(photoDir, "H_06-001_002.jpg"), "bild");

        var stale = Path.Combine(temp.Path, "Importdateien", "XTF", "Foto", "H_06-001_002.jpg");
        var project = BuildProjectWithStalePhoto(stale);
        var repo = new JsonProjectRepository();

        var save = repo.Save(project, projectFile);
        var load = repo.Load(projectFile);

        Assert.True(save.Ok, save.ErrorMessage);
        Assert.True(load.Ok, load.ErrorMessage);
        var record = Assert.Single(load.Value!.Data);
        Assert.Equal("Fotos/Haltungen/06-001/H_06-001_002.jpg", Assert.Single(record.Protocol!.Current.Entries[0].FotoPaths));
        Assert.Equal("Fotos/Haltungen/06-001/H_06-001_002.jpg", Assert.Single(record.VsaFindings).FotoPath);
    }

    [Fact]
    public void Save_NormalizesRenamedHoldingPhotoReferencesByPhotoSuffix()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);

        var oldPhoto = Path.Combine(temp.Path, "Importdateien", "XTF", "Foto", "H_22147-547.01_116.jpg");
        var project = BuildProjectWithRenamedHoldingPhoto(oldPhoto);

        var photoDir = Path.Combine(temp.Path, "Fotos", "Haltungen", "22147-22151");
        Directory.CreateDirectory(photoDir);
        File.WriteAllText(Path.Combine(photoDir, "H_22147-22151_116.jpg"), "bild");

        var repo = new JsonProjectRepository();
        var save = repo.Save(project, projectFile);
        var load = repo.Load(projectFile);

        Assert.True(save.Ok, save.ErrorMessage);
        Assert.True(load.Ok, load.ErrorMessage);
        var record = Assert.Single(load.Value!.Data);
        Assert.Equal("Fotos/Haltungen/22147-22151/H_22147-22151_116.jpg", Assert.Single(record.Protocol!.Current.Entries[0].FotoPaths));
        Assert.Equal("Fotos/Haltungen/22147-22151/H_22147-22151_116.jpg", Assert.Single(record.VsaFindings).FotoPath);
    }

    [Fact]
    public void Save_NormalizesOriginalProtocolPhotoReferences()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);

        var photoDir = Path.Combine(temp.Path, "Fotos", "Haltungen", "06-001");
        Directory.CreateDirectory(photoDir);
        File.WriteAllText(Path.Combine(photoDir, "H_06-001_002.jpg"), "bild");

        var stale = Path.Combine(temp.Path, "Importdateien", "XTF", "Foto", "H_06-001_002.jpg");
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "06-001", FieldSource.Xtf, userEdited: false);
        record.Protocol = new ProtocolDocument();
        record.Protocol.Original.Entries.Add(new ProtocolEntry
        {
            FotoPaths = { stale }
        });
        project.Data.Add(record);

        var repo = new JsonProjectRepository();
        var save = repo.Save(project, projectFile);
        var load = repo.Load(projectFile);

        Assert.True(save.Ok, save.ErrorMessage);
        Assert.True(load.Ok, load.ErrorMessage);
        var loadedRecord = Assert.Single(load.Value!.Data);
        Assert.Equal(
            "Fotos/Haltungen/06-001/H_06-001_002.jpg",
            Assert.Single(loadedRecord.Protocol!.Original.Entries[0].FotoPaths));
    }

    private static Project BuildProjectWithStalePhoto(string stalePath)
    {
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "06-001", FieldSource.Xtf, userEdited: false);
        record.Protocol = new ProtocolDocument();
        var entry = new ProtocolEntry { Code = "BAA" };
        entry.FotoPaths.Add("Fotos/Haltungen/06-001/H_06-001_002.jpg");
        entry.FotoPaths.Add(stalePath);
        record.Protocol.Current.Entries.Add(entry);
        record.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BAA", FotoPath = stalePath });
        project.Data.Add(record);
        return project;
    }

    private static Project BuildProjectWithRenamedHoldingPhoto(string stalePath)
    {
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "22147-22151", FieldSource.Xtf, userEdited: false);
        record.Protocol = new ProtocolDocument();
        var entry = new ProtocolEntry { Code = "BAA" };
        entry.FotoPaths.Add("Fotos/Haltungen/22147-22151/H_22147-22151_116.jpg");
        entry.FotoPaths.Add(stalePath);
        record.Protocol.Current.Entries.Add(entry);
        record.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BAA", FotoPath = stalePath });
        project.Data.Add(record);
        return project;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jsonrepo-photo-" + Guid.NewGuid().ToString("N"));

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
