using System.Text.Json;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class BackupExternalReferencesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "backup-reference-test-" + Guid.NewGuid());

    [Theory]
    [InlineData("semicolon")]
    [InlineData("json")]
    [InlineData("array")]
    public async Task Pdf_Sammlung_sichert_jede_Datei_einzeln_auch_relative_Pfade(string format)
    {
        var sources = Sources();
        var originals = Directory.CreateDirectory(Path.Combine(_root, "originals")).FullName;
        var first = Path.Combine(originals, "a.pdf");
        var second = Path.Combine(originals, "b.pdf");
        File.WriteAllText(first, "Protokoll A");
        File.WriteAllText(second, "Protokoll B");
        var paths = new[] { first, Path.GetRelativePath(sources.ProjectRoots![0], second), first };
        object stored = format switch
        {
            "semicolon" => string.Join(";", paths),
            "json" => JsonSerializer.Serialize(paths),
            _ => paths
        };
        File.WriteAllText(Path.Combine(sources.ProjectRoots[0], "projekt.json"),
            JsonSerializer.Serialize(new { Fields = new Dictionary<string, object> { ["PDF_All"] = stored } }));

        var result = await new FullBackupService(() => sources).RunAsync(Path.Combine(_root, "target"));

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.SkippedFiles);
        var copies = Directory.GetFiles(Path.Combine(result.TargetRoot!, "Externe_Dateien"), "*.pdf", SearchOption.AllDirectories);
        Assert.Equal(2, copies.Length);
        Assert.Contains(copies, file => File.ReadAllText(file) == "Protokoll A");
        Assert.Contains(copies, file => File.ReadAllText(file) == "Protokoll B");
        Assert.True((await BackupManifestIntegrity.VerifyAsync(result.TargetRoot!)).IsValid);
    }

    [Fact]
    public void Einzelpfad_mit_Semikolon_im_Dateinamen_bleibt_erhalten()
    {
        var sources = Sources();
        var path = Path.Combine(_root, "Protokoll;Ergaenzung.pdf");
        File.WriteAllText(path, "Inhalt");
        File.WriteAllText(Path.Combine(sources.ProjectRoots![0], "projekt.json"),
            JsonSerializer.Serialize(new { PDF_Path = path }));

        var resolved = BackupExternalReferences.Resolve(sources, CancellationToken.None);

        Assert.Equal(path, Assert.Single(resolved.ReferencedFiles!).SourcePath);
    }

    private FullBackupSources Sources()
    {
        string Folder(string name) => Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
        return new(Folder("repo"), Folder("brain"), Folder("local"), Folder("roaming"),
            Folder("legacy"), Folder("desktop"), "test", new Dictionary<string, string>(), [Folder("projects")], true);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
