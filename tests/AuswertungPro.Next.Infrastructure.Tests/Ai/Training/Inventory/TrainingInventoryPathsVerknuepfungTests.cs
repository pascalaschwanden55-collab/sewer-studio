using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.Inventory;

/// <summary>
/// Die zentrale Verknuepfungssuche der Trainingsablage (Inventar, Export, Goldpruefung) auf dem
/// gemeinsamen VerknuepfungsSchutz (Deepscan 02.10.2026, A5): ganzer Pfad ab Laufwerk, ein noch
/// fehlender Rest ist erlaubt, Lesefehler werfen weiter.
/// </summary>
public sealed class TrainingInventoryPathsVerknuepfungTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "training_verknuepfung_" + Guid.NewGuid().ToString("N"));

    public TrainingInventoryPathsVerknuepfungTests() => Directory.CreateDirectory(_root);

    [JunctionFact]
    public void FindReparsePoint_meldet_die_Junction_im_Pfad()
    {
        var ziel = Directory.CreateDirectory(Path.Combine(_root, "ziel")).FullName;
        var link = Path.Combine(_root, "link");
        JunctionTestSupport.CreateDirectoryLink(link, ziel);

        Assert.Equal(link, TrainingInventoryPaths.FindReparsePoint(Path.Combine(link, "noch_nicht_da", "bild.png")));
    }

    [Fact]
    public void FindReparsePoint_laesst_normale_und_noch_fehlende_Pfade_zu()
    {
        var ordner = Directory.CreateDirectory(Path.Combine(_root, "a")).FullName;

        Assert.Null(TrainingInventoryPaths.FindReparsePoint(ordner));
        Assert.Null(TrainingInventoryPaths.FindReparsePoint(Path.Combine(ordner, "fehlt", "bild.png")));
    }

    public void Dispose()
    {
        try
        {
            var link = Path.Combine(_root, "link");
            if (Directory.Exists(link))
                Directory.Delete(link);
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }
    }
}
