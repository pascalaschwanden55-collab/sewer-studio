using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

public sealed class ExternalProjectLinkSanitizerTests
{
    [Fact]
    public void SanitizeMetadataLinks_copies_existing_external_logo_into_project()
    {
        using var temp = TempProject.Create();
        var logo = Touch(Path.Combine(temp.Root, "external", "logo.png"), "logo");
        var project = new Project();
        project.Metadata[ModernizerProjectKeys.CustomerLogoPath] = logo;
        var report = new ModernizeReport();

        ExternalProjectLinkSanitizer.SanitizeMetadataLinks(project, temp.ProjectFolder, dryRun: false, report);

        Assert.Equal(
            $"{ProjectStructure.Projektdateien}/{ModernizerProjectKeys.LogosFolder}/logo.png",
            project.Metadata[ModernizerProjectKeys.CustomerLogoPath]);
        Assert.True(File.Exists(Path.Combine(temp.ProjectFolder, ProjectStructure.Projektdateien, ModernizerProjectKeys.LogosFolder, "logo.png")));
        Assert.Equal(1, report.MetadataUpdated);
        Assert.Equal(1, report.ExternalLinksRemoved);
    }

    [Fact]
    public void SanitizeMetadataLinks_removes_missing_external_logo()
    {
        using var temp = TempProject.Create();
        var missingLogo = Path.Combine(temp.Root, "external", "missing.png");
        var project = new Project();
        project.Metadata[ModernizerProjectKeys.CustomerLogoPath] = missingLogo;
        var report = new ModernizeReport();

        ExternalProjectLinkSanitizer.SanitizeMetadataLinks(project, temp.ProjectFolder, dryRun: false, report);

        Assert.Equal("", project.Metadata[ModernizerProjectKeys.CustomerLogoPath]);
        Assert.Equal(1, report.MetadataUpdated);
        Assert.Equal(1, report.ExternalLinksRemoved);
    }

    [Fact]
    public void SanitizeMetadataLinks_rewrites_external_import_metadata()
    {
        using var temp = TempProject.Create();
        var project = new Project();
        project.Metadata[ModernizerProjectKeys.ImportSource] = Path.Combine(temp.Root, "external", "export");
        project.Metadata[ModernizerProjectKeys.ImportSourceHistory] = $"Alt={Path.Combine(temp.Root, "external", "history")}";
        var report = new ModernizeReport();

        ExternalProjectLinkSanitizer.SanitizeMetadataLinks(project, temp.ProjectFolder, dryRun: false, report);

        Assert.Equal(ProjectStructure.Importdateien, project.Metadata[ModernizerProjectKeys.ImportSource]);
        Assert.Equal(ModernizerProjectKeys.ModernizedImportSourceHistory, project.Metadata[ModernizerProjectKeys.ImportSourceHistory]);
        Assert.Equal(2, report.MetadataUpdated);
        Assert.Equal(2, report.ExternalLinksRemoved);
    }

    private static string Touch(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
