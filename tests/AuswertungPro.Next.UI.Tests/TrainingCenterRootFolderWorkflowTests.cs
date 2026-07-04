using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterRootFolderWorkflowTests
{
    [Fact]
    public void ApplySelected_ignoriert_leere_auswahl_ohne_display_update()
    {
        var roots = new List<string> { @"C:\Training\A" };
        var updates = 0;

        TrainingCenterRootFolderWorkflow.ApplySelected(
            roots,
            Array.Empty<string>(),
            () => updates++);

        Assert.Equal(new[] { @"C:\Training\A" }, roots);
        Assert.Equal(0, updates);
    }

    [Fact]
    public void ApplySelected_fuegt_neue_ordner_eindeutig_hinzu_und_aktualisiert_display()
    {
        var roots = new List<string> { @"C:\Training\A" };
        var updates = 0;

        TrainingCenterRootFolderWorkflow.ApplySelected(
            roots,
            new[] { @"c:\training\a", @"D:\Training\B" },
            () => updates++);

        Assert.Equal(new[] { @"C:\Training\A", @"D:\Training\B" }, roots);
        Assert.Equal(1, updates);
    }

    [Fact]
    public void Clear_leert_rootfolders_und_aktualisiert_display()
    {
        var roots = new List<string> { @"C:\Training\A", @"D:\Training\B" };
        var updates = 0;

        TrainingCenterRootFolderWorkflow.Clear(roots, () => updates++);

        Assert.Empty(roots);
        Assert.Equal(1, updates);
    }
}
