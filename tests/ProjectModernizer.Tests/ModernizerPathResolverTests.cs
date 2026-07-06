using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Media;
using Xunit;

public sealed class ModernizerPathResolverTests
{
    [Fact]
    public void TryResolveOrCopyModernPath_resolves_existing_project_relative_modern_file()
    {
        using var temp = TempProject.Create();
        var modernRoot = Path.Combine(temp.ProjectFolder, ProjectStructure.HaltungenVerteilt, "06.1-07.2");
        var video = Touch(Path.Combine(modernRoot, ModernizerLegacyFolders.HoldingVideo, "film.mp4"), "video");
        var raw = ProjectPathResolver.MakeRelative(video, temp.ProjectFolder);
        var report = new ModernizeReport();

        var ok = ModernizerPathResolver.TryResolveOrCopyModernPath(
            raw,
            modernRoot,
            temp.ProjectFolder,
            MediaFileTypes.HasVideoExtension,
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            dryRun: false,
            report,
            FileCopyKind.Haltung,
            out var relative);

        Assert.True(ok);
        Assert.Equal(raw, relative);
        Assert.Equal(0, report.RelinkedPaths);
        Assert.Equal(0, report.HaltungFilesCopied);
    }

    [Fact]
    public void TryResolveOrCopyModernPath_copies_external_candidate_into_typed_subfolder()
    {
        using var temp = TempProject.Create();
        var modernRoot = Path.Combine(temp.ProjectFolder, ProjectStructure.HaltungenVerteilt, "06.1-07.2");
        var external = Touch(Path.Combine(temp.Root, "source", "film.mp4"), "video");
        var externalFiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["film.mp4"] = new() { external }
        };
        var report = new ModernizeReport();

        var ok = ModernizerPathResolver.TryResolveOrCopyModernPath(
            "film.mp4",
            modernRoot,
            temp.ProjectFolder,
            MediaFileTypes.HasVideoExtension,
            externalFiles,
            dryRun: false,
            report,
            FileCopyKind.Haltung,
            out var relative);

        var copied = Path.Combine(modernRoot, ModernizerLegacyFolders.HoldingVideo, "film.mp4");
        Assert.True(ok);
        Assert.Equal(ProjectPathResolver.MakeRelative(copied, temp.ProjectFolder), relative);
        Assert.True(File.Exists(copied));
        Assert.True(File.Exists(external));
        Assert.Equal(1, report.HaltungFilesCopied);
        Assert.Equal(0, report.RelinkedPaths);
    }

    [Fact]
    public void TryResolveOrCopyModernPath_maps_single_legacy_file_to_modern_relative_path()
    {
        using var temp = TempProject.Create();
        var san = "06.1-07.2";
        var modernRoot = Path.Combine(temp.ProjectFolder, ProjectStructure.HaltungenVerteilt, san);
        var legacyRoot = Path.Combine(temp.ProjectFolder, "Haltungen", san);
        Touch(Path.Combine(legacyRoot, ModernizerLegacyFolders.HoldingVideo, "film.mp4"), "video");
        var report = new ModernizeReport();

        var ok = ModernizerPathResolver.TryResolveOrCopyModernPath(
            "film.mp4",
            modernRoot,
            temp.ProjectFolder,
            MediaFileTypes.HasVideoExtension,
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            dryRun: false,
            report,
            FileCopyKind.Haltung,
            out var relative);

        Assert.True(ok);
        Assert.Equal(ProjectPathResolver.MakeRelative(Path.Combine(modernRoot, ModernizerLegacyFolders.HoldingVideo, "film.mp4"), temp.ProjectFolder), relative);
        Assert.Equal(0, report.HaltungFilesCopied);
        Assert.Equal(0, report.RelinkedPaths);
    }

    [Fact]
    public void TryResolveOrCopyModernPath_maps_single_legacy_schacht_file_to_modern_relative_path()
    {
        using var temp = TempProject.Create();
        var schacht = "S1";
        var modernRoot = Path.Combine(temp.ProjectFolder, ProjectStructure.SchaechteVerteilt, schacht);
        var legacyRoot = Path.Combine(temp.ProjectFolder, "Sch\u00e4chte_1.15", schacht);
        Touch(Path.Combine(legacyRoot, ModernizerLegacyFolders.HoldingPdf, "report.pdf"), "pdf");
        var report = new ModernizeReport();

        var ok = ModernizerPathResolver.TryResolveOrCopyModernPath(
            "report.pdf",
            modernRoot,
            temp.ProjectFolder,
            ModernizerStructureFiles.IsPdf,
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            dryRun: false,
            report,
            FileCopyKind.Schacht,
            out var relative);

        Assert.True(ok);
        Assert.Equal(ProjectPathResolver.MakeRelative(Path.Combine(modernRoot, ModernizerLegacyFolders.HoldingPdf, "report.pdf"), temp.ProjectFolder), relative);
        Assert.Equal(0, report.SchachtFilesCopied);
        Assert.Equal(0, report.RelinkedPaths);
    }

    private static string Touch(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
