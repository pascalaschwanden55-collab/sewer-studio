using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterStateControllerTests
{
    [Fact]
    public void RestoreExistingRootFolders_filtert_nicht_vorhandene_ordner_und_erhaelt_reihenfolge()
    {
        var state = new TrainingCenterState
        {
            RootFolders = new List<string>
            {
                @"C:\Training\A",
                @"C:\Training\Fehlt",
                @"D:\Training\B"
            }
        };

        var restored = TrainingCenterStateController.RestoreExistingRootFolders(
            state,
            path => path.EndsWith(@"\A", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(@"\B", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(new[] { @"C:\Training\A", @"D:\Training\B" }, restored);
    }

    [Fact]
    public void AddSelectedRootFolders_fuegt_neue_ordner_case_insensitive_eindeutig_hinzu()
    {
        var roots = new List<string> { @"C:\Training\A" };

        var changed = TrainingCenterStateController.AddSelectedRootFolders(
            roots,
            new[]
            {
                @"c:\training\a",
                @"D:\Training\B",
                @"D:\Training\B"
            });

        Assert.True(changed);
        Assert.Equal(new[] { @"C:\Training\A", @"D:\Training\B" }, roots);
    }

    [Fact]
    public void ReplaceRootFolders_ersetzt_alle_ordner_mit_neuer_liste()
    {
        var roots = new List<string> { @"C:\Training\Alt" };

        TrainingCenterStateController.ReplaceRootFolders(
            roots,
            new[] { @"C:\Training\A", @"D:\Training\B" });

        Assert.Equal(new[] { @"C:\Training\A", @"D:\Training\B" }, roots);
    }

    [Fact]
    public void AddRootFolder_fuegt_case_insensitive_eindeutig_hinzu()
    {
        var roots = new List<string> { @"C:\Training\A" };

        var addedDuplicate = TrainingCenterStateController.AddRootFolder(roots, @"c:\training\a");
        var addedNew = TrainingCenterStateController.AddRootFolder(roots, @"D:\Training\B");

        Assert.False(addedDuplicate);
        Assert.True(addedNew);
        Assert.Equal(new[] { @"C:\Training\A", @"D:\Training\B" }, roots);
    }

    [Fact]
    public void BuildState_kopiert_cases_und_rootfolders_mit_zeitstempel()
    {
        var updatedUtc = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        var cases = new List<TrainingCase>
        {
            new() { CaseId = "1.1-1.2" }
        };
        var roots = new List<string> { @"C:\Training\A" };

        var state = TrainingCenterStateController.BuildState(cases, roots, updatedUtc);

        Assert.Equal(updatedUtc, state.UpdatedUtc);
        Assert.Equal(cases, state.Cases);
        Assert.Equal(roots, state.RootFolders);
        Assert.NotSame(cases, state.Cases);
        Assert.NotSame(roots, state.RootFolders);
    }
}
