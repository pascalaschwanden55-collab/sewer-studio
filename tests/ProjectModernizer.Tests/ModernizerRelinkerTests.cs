using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

public sealed class ModernizerRelinkerTests
{
    [Fact]
    public void RelinkHaltungenIgnoresNullVsaFindings()
    {
        using var temp = TempProject.Create();
        var record = new HaltungRecord
        {
            VsaFindings = null!
        };
        record.SetFieldValue(FieldKeys.HoldingName, "06.1-07.2", FieldSource.Manual, userEdited: false);
        var project = new Project
        {
            Data = { record }
        };
        var report = new ModernizeReport();

        ModernizerRelinker.RelinkHaltungen(
            project,
            temp.ProjectFolder,
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            dryRun: true,
            report);

        Assert.Equal(0, report.CopyErrors);
    }

    [Fact]
    public void RelinkSchaechteCopiesProtocolPhotosAsSchachtFiles()
    {
        using var temp = TempProject.Create();
        var source = Touch(Path.Combine(temp.Root, "external", "photo.jpg"), "photo");
        var schacht = new SchachtRecord
        {
            Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries =
                    {
                        new ProtocolEntry
                        {
                            FotoPaths = { source }
                        }
                    }
                }
            }
        };
        schacht.SetFieldValue(ModernizerProjectKeys.SchachtNumberFields[0], "S-001");
        var project = new Project
        {
            SchaechteData = { schacht }
        };
        var report = new ModernizeReport();
        var externalFiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetFileName(source)] = new() { source }
        };

        ModernizerRelinker.RelinkSchaechte(project, temp.ProjectFolder, externalFiles, dryRun: false, report);

        var expected = Path.Combine(temp.ProjectFolder, ProjectStructure.SchaechteVerteilt, "S-001", "Fotos", "photo.jpg");
        Assert.True(File.Exists(expected));
        Assert.Equal(ProjectPathResolver.MakeRelative(expected, temp.ProjectFolder), schacht.Protocol.Current.Entries[0].FotoPaths[0]);
        Assert.Equal(1, report.SchachtFilesCopied);
        Assert.Equal(0, report.HaltungFilesCopied);
        Assert.Equal(1, report.RelinkedPaths);
    }

    [Fact]
    public void RelinkHaltungenCopiesSingleSourceVideoWhenHoldingHasNoVideo()
    {
        using var temp = TempProject.Create();
        var haltung = "06.1-07.2";
        var source = Touch(Path.Combine(temp.Root, "source", $"{haltung}_GUID.mp4"), "video");
        var record = CreateHoldingRecord(haltung);
        var project = new Project
        {
            Data = { record }
        };
        var report = new ModernizeReport();
        var sourceVideos = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [haltung] = new() { source }
        };

        ModernizerRelinker.RelinkHaltungen(
            project,
            temp.ProjectFolder,
            sourceVideos,
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            dryRun: false,
            report);

        var expected = Path.Combine(temp.ProjectFolder, ProjectStructure.HaltungenVerteilt, haltung, ModernizerLegacyFolders.HoldingVideo, Path.GetFileName(source));
        Assert.True(File.Exists(expected));
        Assert.Equal(ProjectPathResolver.MakeRelative(expected, temp.ProjectFolder), record.GetFieldValue(FieldKeys.Link));
        Assert.Equal(1, report.HaltungFilesCopied);
        Assert.Equal(1, report.RelinkedPaths);
    }

    [Fact]
    public void RelinkHaltungenSkipsAmbiguousSourceVideos()
    {
        using var temp = TempProject.Create();
        var haltung = "06.1-07.2";
        var first = Touch(Path.Combine(temp.Root, "source", $"{haltung}_A.mp4"), "video-a");
        var second = Touch(Path.Combine(temp.Root, "source", $"{haltung}_B.mp4"), "video-b");
        var record = CreateHoldingRecord(haltung);
        var project = new Project
        {
            Data = { record }
        };
        var report = new ModernizeReport();
        var sourceVideos = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [haltung] = new() { first, second }
        };

        ModernizerRelinker.RelinkHaltungen(
            project,
            temp.ProjectFolder,
            sourceVideos,
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            dryRun: false,
            report);

        Assert.Equal("", record.GetFieldValue(FieldKeys.Link));
        Assert.Equal(0, report.HaltungFilesCopied);
        Assert.Equal(0, report.RelinkedPaths);
    }

    [Fact]
    public void RelinkHaltungenDryRunReportsSingleSourceVideoWithoutChangingRecord()
    {
        using var temp = TempProject.Create();
        var haltung = "06.1-07.2";
        var source = Touch(Path.Combine(temp.Root, "source", $"{haltung}_GUID.mp4"), "video");
        var record = CreateHoldingRecord(haltung);
        var project = new Project
        {
            Data = { record }
        };
        var report = new ModernizeReport();
        var sourceVideos = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [haltung] = new() { source }
        };

        ModernizerRelinker.RelinkHaltungen(
            project,
            temp.ProjectFolder,
            sourceVideos,
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            dryRun: true,
            report);

        var expected = Path.Combine(temp.ProjectFolder, ProjectStructure.HaltungenVerteilt, haltung, ModernizerLegacyFolders.HoldingVideo, Path.GetFileName(source));
        Assert.False(File.Exists(expected));
        Assert.Equal("", record.GetFieldValue(FieldKeys.Link));
        Assert.Equal(1, report.HaltungFilesCopied);
        Assert.Equal(1, report.RelinkedPaths);
    }

    private static string Touch(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static HaltungRecord CreateHoldingRecord(string holdingName)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, holdingName, FieldSource.Manual, userEdited: false);
        return record;
    }
}
