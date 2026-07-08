using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

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
        // Draft-Projekt wird jetzt ueber die Factory erzeugt (Auftraggeber-Default "Abwasser Uri").
        Assert.Contains("NewProjectDraftFactory.Create()", src);
        Assert.Contains("public bool CreateProjectFromDraft", src);
        Assert.Contains("public void EnterWorkspaceOn", src);
        Assert.Contains("NewProjectFolderPlanner.Plan", src);
        Assert.Contains("SelectFolder(\"Projekte-Verzeichnis waehlen\", @\"D:\\Projekt\")", src);
    }

}
