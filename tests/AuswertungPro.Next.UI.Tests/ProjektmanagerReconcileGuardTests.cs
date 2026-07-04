using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektmanagerReconcileGuardTests
{
    [Fact]
    public void Manueller_Import_verwendet_MediaDistribution_ohne_Videokopie()
    {
        var src = ReadRepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ImportPageViewModel.cs");

        Assert.Contains("includeVideos: false", src);
        Assert.Contains("Fotos/PDFs", src);
    }

    [Fact]
    public void Export_Verteilung_nimmt_standardmaessig_den_Projektordner()
    {
        var src = ReadRepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ExportPageViewModel.cs");

        Assert.Contains("DistributionTargetFolderPolicy.Resolve", src);
        Assert.Contains("_shell.GetProjectFolder()", src);
    }

    [Fact]
    public void Shell_Projekterzeugung_nutzt_neue_Struktur_und_ProjectFileLocator()
    {
        var src = ReadRepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs");

        Assert.Contains("NewProjectFolderPlanner.Plan", src);
        Assert.Contains("ProjectStructure.EnsureCreated", src);
        Assert.Contains("ProjectFileLocator.TargetPath", src);
    }

    private static string ReadRepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(parts));
    }
}
