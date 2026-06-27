using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektEroeffnungShellGuardTests
{
    private static string ShellSource()
        => File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));

    [Fact]
    public void Shell_defines_three_modes_and_menu_visibility()
    {
        var src = ShellSource();
        Assert.Contains("enum ShellMode", src);
        Assert.Contains("Launcher", src);
        Assert.Contains("Draft", src);
        Assert.Contains("Workspace", src);
        Assert.Contains("public bool IsMenuVisible", src);
    }

    [Fact]
    public void Shell_has_switch_and_draft_flow()
    {
        var src = ShellSource();
        Assert.Contains("SwitchProjectCommand", src);
        Assert.Contains("public void StartNewProjectDraft", src);
        Assert.Contains("new Project { Name = string.Empty }", src);
        Assert.Contains("public bool CreateProjectFromDraft", src);
        Assert.Contains("public void EnterWorkspaceOn", src);
        Assert.Contains("NewProjectFolderPlanner.Plan", src);
        Assert.Contains("SelectFolder(\"Projekte-Verzeichnis waehlen\", @\"D:\\Projekt\")", src);
    }

    [Fact]
    public void Shell_no_longer_registers_uebersicht_navitem()
    {
        var src = ShellSource();
        Assert.DoesNotContain("\"Uebersicht\", () => new Pages.OverviewPageViewModel", src);
    }

    internal static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(parts));
    }
}
