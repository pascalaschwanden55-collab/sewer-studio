using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class JsonProjectRepositoryRobustnessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "project-robustness-" + Guid.NewGuid().ToString("N"));

    public JsonProjectRepositoryRobustnessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_root, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void Save_AufSchreibgeschuetzteDatei_LaesstAltenInhaltUnveraendert()
    {
        var path = Path.Combine(_root, "projekt.json");
        File.WriteAllText(path, "alter-inhalt");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        var result = new JsonProjectRepository().Save(new Project { Name = "Neu" }, path);

        Assert.False(result.Ok);
        Assert.Equal("APP-SAVE", result.ErrorCode);
        Assert.Equal("alter-inhalt", File.ReadAllText(path));
    }

    [Fact]
    public void Load_AbgeschnittenesJson_LiefertSauberenFehler()
    {
        var path = Path.Combine(_root, "projekt.json");
        File.WriteAllText(path, "{ \"Name\": \"Unvollstaendig");

        var result = new JsonProjectRepository().Load(path);

        Assert.False(result.Ok);
        Assert.Equal("APP-LOAD", result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Save_ZweiterStand_LegtBakMitVorherigemInhaltAn()
    {
        var path = Path.Combine(_root, "projekt.json");
        var repository = new JsonProjectRepository();
        Assert.True(repository.Save(new Project { Name = "Vorher" }, path).Ok);
        var previousBytes = File.ReadAllBytes(path);

        Assert.True(repository.Save(new Project { Name = "Nachher" }, path).Ok);

        Assert.True(File.Exists(path + ".bak"));
        Assert.Equal(previousBytes, File.ReadAllBytes(path + ".bak"));
    }

    [Fact]
    public void SaveLoad_VollstaendigerHaltungsstand_BleibtErhalten()
    {
        var path = Path.Combine(_root, "projekt.json");
        var photo = Path.Combine(_root, "Fotos", "schaden.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(photo)!);
        File.WriteAllText(photo, "foto");

        var record = new HaltungRecord();
        var sources = Enum.GetValues<FieldSource>();
        for (var i = 0; i < sources.Length; i++)
            record.SetFieldValue($"Testfeld_{i}", $"Wert_{i}", sources[i], userEdited: i % 2 == 0);

        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BAB",
            MeterStart = 12.3,
            MeterEnd = 13.1,
            FotoPath = photo,
            EZD = 3,
            Raw = "Riss"
        });
        record.Protocol = new ProtocolDocument
        {
            HaltungId = "H-1",
            Original = Revision("BAB", photo),
            Current = Revision("BAA", photo)
        };
        record.Protocol.History.Add(Revision("BCA", photo));

        var project = new Project { Name = "Roundtrip" };
        project.Data.Add(record);
        var repository = new JsonProjectRepository();

        Assert.True(repository.Save(project, path).Ok);
        var loadedResult = repository.Load(path);
        Assert.True(loadedResult.Ok, loadedResult.ErrorMessage);
        var loaded = Assert.Single(loadedResult.Value!.Data);

        for (var i = 0; i < sources.Length; i++)
        {
            Assert.Equal($"Wert_{i}", loaded.GetFieldValue($"Testfeld_{i}"));
            Assert.Equal(sources[i], loaded.FieldMeta[$"Testfeld_{i}"].Source);
            Assert.Equal(i % 2 == 0, loaded.FieldMeta[$"Testfeld_{i}"].UserEdited);
        }

        var finding = Assert.Single(loaded.VsaFindings);
        Assert.Equal("BAB", finding.KanalSchadencode);
        Assert.Equal(12.3, finding.MeterStart);
        Assert.True(File.Exists(finding.FotoPath));
        Assert.Equal("BAB", Assert.Single(loaded.Protocol!.Original.Entries).Code);
        Assert.Equal("BAA", Assert.Single(loaded.Protocol.Current.Entries).Code);
        Assert.Equal("BCA", Assert.Single(Assert.Single(loaded.Protocol.History).Entries).Code);
    }

    private static ProtocolRevision Revision(string code, string photo)
        => new()
        {
            Comment = code,
            Entries =
            [
                new ProtocolEntry
                {
                    Code = code,
                    Beschreibung = "Test",
                    MeterStart = 1.2,
                    FotoPaths = [photo],
                    Source = ProtocolEntrySource.Imported
                }
            ]
        };
}
