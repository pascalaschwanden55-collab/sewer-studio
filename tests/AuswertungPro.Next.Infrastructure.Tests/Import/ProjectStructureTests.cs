using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer die zentrale Projekt-Ordnerstruktur (ProjectStructure).
/// </summary>
public sealed class ProjectStructureTests
{
    [Fact]
    public void EnsureCreated_CreatesAllFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ps-{Guid.NewGuid():N}");
        try
        {
            ProjectStructure.EnsureCreated(root);
            Assert.True(Directory.Exists(Path.Combine(root, "Importdateien", "Datenbanken")));
            Assert.True(Directory.Exists(Path.Combine(root, "Importdateien", "XTF")));
            Assert.True(Directory.Exists(Path.Combine(root, "Importdateien", "PDF")));
            Assert.True(Directory.Exists(Path.Combine(root, "Importdateien", "TXT")));
            Assert.True(Directory.Exists(Path.Combine(root, "Haltungen_Verteilt")));
            Assert.True(Directory.Exists(Path.Combine(root, "Schächte_Verteilt")));
            Assert.True(Directory.Exists(Path.Combine(root, "Fotos", "Haltungen")));
            Assert.True(Directory.Exists(Path.Combine(root, "Fotos", "Schächte")));
            Assert.True(Directory.Exists(Path.Combine(root, "Projektdateien")));
            Assert.True(Directory.Exists(Path.Combine(root, "__IMPORT_REPORTS")));
            Assert.True(Directory.Exists(Path.Combine(root, "__RESTORE_POINTS")));
            // Zweiter Aufruf darf nicht werfen (idempotent)
            ProjectStructure.EnsureCreated(root);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void FotosHaltungDir_ReturnsGroupedPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ps-{Guid.NewGuid():N}");
        Assert.Equal(
            Path.Combine(root, "Fotos", "Haltungen", "06-001"),
            ProjectStructure.FotosHaltungDir(root, "06-001"));
    }

    [Fact]
    public void FotosSchachtDir_ReturnsGroupedPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ps-{Guid.NewGuid():N}");
        Assert.Equal(
            Path.Combine(root, "Fotos", "Schächte", "S-42"),
            ProjectStructure.FotosSchachtDir(root, "S-42"));
    }

    [Fact]
    public void HaltungVerteiltDir_ReturnsCorrectPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ps-{Guid.NewGuid():N}");
        Assert.Equal(
            Path.Combine(root, "Haltungen_Verteilt", "H-123"),
            ProjectStructure.HaltungVerteiltDir(root, "H-123"));
    }

    [Fact]
    public void SchachtVerteiltDir_ReturnsCorrectPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ps-{Guid.NewGuid():N}");
        Assert.Equal(
            Path.Combine(root, "Schächte_Verteilt", "S-99"),
            ProjectStructure.SchachtVerteiltDir(root, "S-99"));
    }

    [Fact]
    public void ImportdateienDir_Datenbanken_ReturnsCorrectPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ps-{Guid.NewGuid():N}");
        Assert.Equal(
            Path.Combine(root, "Importdateien", "Datenbanken"),
            ProjectStructure.ImportdateienDir(root, "Datenbanken"));
    }

    [Fact]
    public void ImportdateienDir_Xtf_ReturnsCorrectPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ps-{Guid.NewGuid():N}");
        Assert.Equal(
            Path.Combine(root, "Importdateien", "XTF"),
            ProjectStructure.ImportdateienDir(root, "XTF"));
    }

    [Fact]
    public void Konstanten_HabenKorrekteBenennung()
    {
        Assert.Equal("Importdateien", ProjectStructure.Importdateien);
        Assert.Equal("Datenbanken", ProjectStructure.Datenbanken);
        Assert.Equal("XTF", ProjectStructure.XtfDir);
        Assert.Equal("PDF", ProjectStructure.PdfDir);
        Assert.Equal("TXT", ProjectStructure.TxtDir);
        Assert.Equal("Haltungen_Verteilt", ProjectStructure.HaltungenVerteilt);
        Assert.Equal("Schächte_Verteilt", ProjectStructure.SchaechteVerteilt);
        Assert.Equal("Fotos", ProjectStructure.Fotos);
        Assert.Equal("Haltungen", ProjectStructure.FotosHaltungen);
        Assert.Equal("Schächte", ProjectStructure.FotosSchaechte);
        Assert.Equal("Projektdateien", ProjectStructure.Projektdateien);
        Assert.Equal("__IMPORT_REPORTS", ProjectStructure.ImportReports);
        Assert.Equal("__RESTORE_POINTS", ProjectStructure.RestorePoints);
    }
}
