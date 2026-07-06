using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

public sealed class ProtocolSnapshotLinkSanitizerTests
{
    [Fact]
    public void SanitizeProtocolChangeSnapshots_replaces_external_photo_with_central_relative_path()
    {
        using var temp = TempProject.Create();
        var haltung = "06.1-07.2";
        var photoName = "schaden.jpg";
        var centralPhoto = Touch(
            Path.Combine(temp.ProjectFolder, ProjectStructure.Fotos, ProjectStructure.FotosHaltungen, haltung, photoName),
            "photo");
        var externalPhoto = Path.Combine(temp.Root, "export", photoName);
        var project = CreateProjectWithSnapshot(haltung, $"vorher {externalPhoto}");
        var report = new ModernizeReport();

        ProtocolSnapshotLinkSanitizer.SanitizeProtocolChangeSnapshots(project, temp.ProjectFolder, dryRun: false, report);

        var snapshot = project.Data[0].Protocol!.Current.Changes[0].Before;
        Assert.NotNull(snapshot);
        Assert.Contains(ProjectPathResolver.MakeRelative(centralPhoto, temp.ProjectFolder), snapshot);
        Assert.DoesNotContain(externalPhoto, snapshot);
        Assert.Equal(1, report.ExternalLinksRemoved);
        Assert.Equal(1, report.SnapshotLinksRemoved);
    }

    [Fact]
    public void SanitizeProtocolChangeSnapshots_removes_external_photo_when_photo_is_not_in_project()
    {
        using var temp = TempProject.Create();
        var externalPhoto = Path.Combine(temp.Root, "export", "missing.jpg");
        var project = CreateProjectWithSnapshot("06.1-07.2", $"nachher {externalPhoto}");
        var report = new ModernizeReport();

        ProtocolSnapshotLinkSanitizer.SanitizeProtocolChangeSnapshots(project, temp.ProjectFolder, dryRun: false, report);

        var snapshot = project.Data[0].Protocol!.Current.Changes[0].Before;
        Assert.Equal("nachher ", snapshot);
        Assert.Equal(1, report.ExternalLinksRemoved);
        Assert.Equal(1, report.SnapshotLinksRemoved);
        Assert.Equal(1, report.UnresolvedPaths);
        Assert.Contains(report.Messages, message => message.Contains("Snapshot-Foto nicht gefunden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SanitizeProtocolChangeSnapshots_keeps_snapshot_unchanged_in_dry_run()
    {
        using var temp = TempProject.Create();
        var externalPhoto = Path.Combine(temp.Root, "export", "missing.jpg");
        var project = CreateProjectWithSnapshot("06.1-07.2", $"nachher {externalPhoto}");
        var report = new ModernizeReport();

        ProtocolSnapshotLinkSanitizer.SanitizeProtocolChangeSnapshots(project, temp.ProjectFolder, dryRun: true, report);

        Assert.Equal($"nachher {externalPhoto}", project.Data[0].Protocol!.Current.Changes[0].Before);
        Assert.Equal(1, report.ExternalLinksRemoved);
        Assert.Equal(1, report.SnapshotLinksRemoved);
    }

    [Fact]
    public void SanitizeProtocolChangeSnapshots_replaces_multiple_external_photos_independently()
    {
        using var temp = TempProject.Create();
        var haltung = "06.1-07.2";
        var first = Touch(
            Path.Combine(temp.ProjectFolder, ProjectStructure.Fotos, ProjectStructure.FotosHaltungen, haltung, "a.jpg"),
            "a");
        var second = Touch(
            Path.Combine(temp.ProjectFolder, ProjectStructure.Fotos, ProjectStructure.FotosHaltungen, haltung, "b.jpg"),
            "b");
        var project = CreateProjectWithSnapshot(haltung, @"vorher D:\Export\a.jpg; nachher D:\Export\b.jpg");
        var report = new ModernizeReport();

        ProtocolSnapshotLinkSanitizer.SanitizeProtocolChangeSnapshots(project, temp.ProjectFolder, dryRun: false, report);

        var snapshot = project.Data[0].Protocol!.Current.Changes[0].Before;
        Assert.NotNull(snapshot);
        Assert.Contains(ProjectPathResolver.MakeRelative(first, temp.ProjectFolder), snapshot);
        Assert.Contains(ProjectPathResolver.MakeRelative(second, temp.ProjectFolder), snapshot);
        Assert.DoesNotContain(@"D:\Export", snapshot);
        Assert.Equal(2, report.ExternalLinksRemoved);
        Assert.Equal(2, report.SnapshotLinksRemoved);
    }

    [Fact]
    public void SanitizeProtocolChangeSnapshots_covers_after_original_and_history()
    {
        using var temp = TempProject.Create();
        var haltung = "06.1-07.2";
        var photo = Touch(
            Path.Combine(temp.ProjectFolder, ProjectStructure.Fotos, ProjectStructure.FotosHaltungen, haltung, "foto.jpg"),
            "photo");
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, haltung, FieldSource.Manual, userEdited: false);
        record.Protocol = new ProtocolDocument
        {
            Original = new ProtocolRevision
            {
                Changes = { new ProtocolChange { After = @"alt D:\Export\foto.jpg" } }
            },
            Current = new ProtocolRevision(),
            History =
            {
                new ProtocolRevision
                {
                    Changes = { new ProtocolChange { Before = @"hist D:\Export\foto.jpg" } }
                }
            }
        };
        var project = new Project();
        project.Data.Add(record);
        var report = new ModernizeReport();

        ProtocolSnapshotLinkSanitizer.SanitizeProtocolChangeSnapshots(project, temp.ProjectFolder, dryRun: false, report);

        var relative = ProjectPathResolver.MakeRelative(photo, temp.ProjectFolder);
        Assert.Contains(relative, record.Protocol.Original.Changes[0].After);
        Assert.Contains(relative, record.Protocol.History[0].Changes[0].Before);
        Assert.Equal(2, report.SnapshotLinksRemoved);
    }

    private static Project CreateProjectWithSnapshot(string haltung, string snapshot)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, haltung, FieldSource.Manual, userEdited: false);
        record.Protocol = new ProtocolDocument
        {
            HaltungId = haltung,
            Current = new ProtocolRevision
            {
                Changes = new List<ProtocolChange>
                {
                    new() { Before = snapshot }
                }
            }
        };

        var project = new Project();
        project.Data.Add(record);
        return project;
    }

    private static string Touch(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
