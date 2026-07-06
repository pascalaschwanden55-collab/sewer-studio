using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

public sealed class ModernizerFlattenerTests
{
    [Fact]
    public void FlattenHaltungenVerteiltCopiesNestedFilesAndKeepsLegacySubfolders()
    {
        using var temp = TempProject.Create();
        var haltung = "06.1-07.2";
        var holdingRoot = Path.Combine(temp.ProjectFolder, ProjectStructure.HaltungenVerteilt, haltung);
        var video = Touch(Path.Combine(holdingRoot, ModernizerLegacyFolders.HoldingVideo, "film.mp4"), "video");
        var pdf = Touch(Path.Combine(holdingRoot, ModernizerLegacyFolders.HoldingPdf, "bericht.pdf"), "pdf");
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, haltung, FieldSource.Manual, userEdited: false);
        record.SetFieldValue(FieldKeys.Link, ProjectPathResolver.MakeRelative(video, temp.ProjectFolder), FieldSource.Manual, userEdited: false);
        record.SetFieldValue(FieldKeys.PdfPath, ProjectPathResolver.MakeRelative(pdf, temp.ProjectFolder), FieldSource.Manual, userEdited: false);
        var project = new Project { Data = { record } };
        var report = new ModernizeReport();

        ModernizerFlattener.FlattenHaltungenVerteilt(project, temp.ProjectFolder, dryRun: false, report);

        var flatVideo = Path.Combine(holdingRoot, "00000000_06.1-07.2.mp4");
        var flatPdf = Path.Combine(holdingRoot, "00000000_06.1-07.2.pdf");
        Assert.True(File.Exists(flatVideo));
        Assert.True(File.Exists(flatPdf));
        Assert.True(File.Exists(video));
        Assert.True(File.Exists(pdf));
        Assert.True(Directory.Exists(Path.Combine(holdingRoot, ModernizerLegacyFolders.HoldingVideo)));
        Assert.True(Directory.Exists(Path.Combine(holdingRoot, ModernizerLegacyFolders.HoldingPdf)));
        Assert.Equal(ProjectPathResolver.MakeRelative(flatVideo, temp.ProjectFolder), record.GetFieldValue(FieldKeys.Link));
        Assert.Equal(ProjectPathResolver.MakeRelative(flatPdf, temp.ProjectFolder), record.GetFieldValue(FieldKeys.PdfPath));
    }

    private static string Touch(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
