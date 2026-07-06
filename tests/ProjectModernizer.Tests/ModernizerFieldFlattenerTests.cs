using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Media;
using Xunit;

public sealed class ModernizerFieldFlattenerTests
{
    [Fact]
    public void FlattenRecordField_copies_nested_video_to_flat_holding_root_and_updates_link()
    {
        using var temp = TempProject.Create();
        var san = "06.1-07.2";
        var holdingRoot = Path.Combine(temp.ProjectFolder, ProjectStructure.HaltungenVerteilt, san);
        var nested = Touch(Path.Combine(holdingRoot, ModernizerLegacyFolders.HoldingVideo, "film.mp4"), "video");
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.Link, ProjectPathResolver.MakeRelative(nested, temp.ProjectFolder), FieldSource.Manual, userEdited: false);
        var report = new ModernizeReport();
        var context = CreateContext(temp.ProjectFolder, holdingRoot, san, "20250131", report);

        ModernizerFieldFlattener.FlattenRecordField(
            record,
            FieldKeys.Link,
            context,
            MediaFileTypes.HasVideoExtension);

        var flat = Path.Combine(holdingRoot, "20250131_06.1-07.2.mp4");
        Assert.True(File.Exists(nested));
        Assert.True(File.Exists(flat));
        Assert.Equal(ProjectPathResolver.MakeRelative(flat, temp.ProjectFolder), record.GetFieldValue(FieldKeys.Link));
        Assert.Equal(1, report.FlattenedFiles);
        Assert.Equal(1, report.RelinkedPaths);
    }

    [Fact]
    public void FlattenRecordField_keeps_direct_child_file_and_rewrites_absolute_path_to_relative()
    {
        using var temp = TempProject.Create();
        var san = "06.1-07.2";
        var holdingRoot = Path.Combine(temp.ProjectFolder, ProjectStructure.HaltungenVerteilt, san);
        var direct = Touch(Path.Combine(holdingRoot, "film.mp4"), "video");
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.Link, direct, FieldSource.Manual, userEdited: false);
        var report = new ModernizeReport();
        var context = CreateContext(temp.ProjectFolder, holdingRoot, san, "20250131", report);

        ModernizerFieldFlattener.FlattenRecordField(
            record,
            FieldKeys.Link,
            context,
            MediaFileTypes.HasVideoExtension);

        Assert.True(File.Exists(direct));
        Assert.Equal(ProjectPathResolver.MakeRelative(direct, temp.ProjectFolder), record.GetFieldValue(FieldKeys.Link));
        Assert.Equal(0, report.FlattenedFiles);
        Assert.Equal(1, report.RelinkedPaths);
    }

    [Fact]
    public void FlattenRecordFieldList_preserves_unresolved_entries_and_reports_them()
    {
        using var temp = TempProject.Create();
        var san = "06.1-07.2";
        var holdingRoot = Path.Combine(temp.ProjectFolder, ProjectStructure.HaltungenVerteilt, san);
        var pdf = Touch(Path.Combine(holdingRoot, ModernizerLegacyFolders.HoldingPdf, "bericht.pdf"), "pdf");
        var missing = "missing.pdf";
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.PdfAll, $"{ProjectPathResolver.MakeRelative(pdf, temp.ProjectFolder)};{missing}", FieldSource.Manual, userEdited: false);
        var report = new ModernizeReport();
        var context = CreateContext(temp.ProjectFolder, holdingRoot, san, "20250131", report);

        ModernizerFieldFlattener.FlattenRecordFieldList(
            record,
            FieldKeys.PdfAll,
            context,
            ModernizerStructureFiles.IsPdf);

        var flat = Path.Combine(holdingRoot, "20250131_06.1-07.2.pdf");
        Assert.True(File.Exists(pdf));
        Assert.True(File.Exists(flat));
        Assert.Equal($"{ProjectPathResolver.MakeRelative(flat, temp.ProjectFolder)};{missing}", record.GetFieldValue(FieldKeys.PdfAll));
        Assert.Equal(1, report.FlattenedFiles);
        Assert.Equal(1, report.UnresolvedPaths);
        Assert.Contains(report.Messages, message => message.Contains(missing, StringComparison.OrdinalIgnoreCase));
    }

    private static string Touch(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static ModernizerFlattenContext CreateContext(
        string projectFolder,
        string holdingRoot,
        string san,
        string stamp,
        ModernizeReport report)
        => new(
            holdingRoot,
            projectFolder,
            san,
            stamp,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            DryRun: false,
            report);
}
