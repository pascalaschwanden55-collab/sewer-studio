using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class ProjectVideoReferenceNormalizerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "video-normalizer-" + Guid.NewGuid().ToString("N"));

    public ProjectVideoReferenceNormalizerTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Save_StoresProjectVideoRelative_AndKeepsExternalVideoAbsolute()
    {
        var projectFile = Path.Combine(_root, "Projektdateien", "projekt.json");
        var projectVideo = Path.Combine(_root, "Haltungen", "100-200", "Video", "film.mp4");
        var externalVideo = Path.Combine(Path.GetTempPath(), "external-" + Guid.NewGuid().ToString("N") + ".mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(projectVideo)!);
        File.WriteAllText(projectVideo, "video");

        var project = new Project();
        var inside = new HaltungRecord();
        inside.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, false);
        inside.SetFieldValue("Link", projectVideo, FieldSource.Legacy, true);
        var outside = new HaltungRecord();
        outside.SetFieldValue("Haltungsname", "200-300", FieldSource.Manual, false);
        outside.SetFieldValue("Link", externalVideo, FieldSource.Legacy, false);
        project.Data.Add(inside);
        project.Data.Add(outside);

        var result = new JsonProjectRepository().Save(project, projectFile);

        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal("Haltungen/100-200/Video/film.mp4", inside.GetFieldValue("Link"));
        Assert.True(inside.FieldMeta["Link"].UserEdited);
        Assert.Equal(externalVideo, outside.GetFieldValue("Link"));

        using var json = JsonDocument.Parse(File.ReadAllText(projectFile));
        var storedLink = json.RootElement.GetProperty("Data")[0].GetProperty("Fields").GetProperty("Link").GetString();
        Assert.Equal("Haltungen/100-200/Video/film.mp4", storedLink);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
