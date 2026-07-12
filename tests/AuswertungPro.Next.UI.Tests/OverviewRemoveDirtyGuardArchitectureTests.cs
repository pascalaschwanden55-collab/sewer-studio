using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OverviewRemoveDirtyGuardArchitectureTests
{
    [Fact]
    public void ActiveProjectRemoval_UsesSharedUnsavedChangesGuardBeforeHiding()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var path = Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "OverviewPageViewModel.cs");
        var source = File.ReadAllText(path);

        var guard = source.IndexOf("_shell.ConfirmDiscardUnsavedChanges()", StringComparison.Ordinal);
        var hide = source.IndexOf("_sp.Settings.HideProject(entry.Path)", StringComparison.Ordinal);
        Assert.True(guard >= 0 && hide > guard, "Dirty-Pruefung muss vor dem Ausblenden des Projekts stehen.");
    }
}
