using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Projektdatei-Suche fuer die Projektliste: findet Alt-Projekte (JSON direkt im
/// Projektordner) UND neue Struktur (&lt;Projekt&gt;\Projektdateien\projekt.json).
/// Hintergrund: Projekte unter D:\Projekte waren in der Uebersicht unsichtbar,
/// weil der alte Scan nur *.json eine Ebene tief suchte.
/// </summary>
public sealed class ProjectFileDiscoveryTests : IDisposable
{
    private readonly string _baseDir;

    public ProjectFileDiscoveryTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "ProjectFileDiscoveryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_baseDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_baseDir, recursive: true); } catch { }
    }

    [Fact]
    public void FindProjectFiles_FindetAltProjektJsonImUnterordner()
    {
        // Alt-Struktur: D:\Projekte\Zone 1.15\Altdorf_Zone_1.15.json
        var projektDir = Directory.CreateDirectory(Path.Combine(_baseDir, "Zone 1.15")).FullName;
        var projektJson = Path.Combine(projektDir, "Altdorf_Zone_1.15.json");
        File.WriteAllText(projektJson, "{}");

        var found = ProjectFileDiscovery.FindProjectFiles(new[] { _baseDir });

        Assert.Contains(projektJson, found);
    }

    [Fact]
    public void FindProjectFiles_FindetNeueStrukturProjektdateienProjektJson()
    {
        // Neue Struktur: D:\Projekte\Fuerlauwi\Projektdateien\projekt.json
        var projektDir = Directory.CreateDirectory(Path.Combine(_baseDir, "Fuerlauwi")).FullName;
        var unterordner = Directory.CreateDirectory(Path.Combine(projektDir, "Projektdateien")).FullName;
        var projektJson = Path.Combine(unterordner, "projekt.json");
        File.WriteAllText(projektJson, "{}");

        var found = ProjectFileDiscovery.FindProjectFiles(new[] { _baseDir });

        Assert.Contains(projektJson, found);
    }

    [Fact]
    public void FindProjectFiles_FindetJsonDirektImBasisordner()
    {
        var projektJson = Path.Combine(_baseDir, "Direkt.json");
        File.WriteAllText(projektJson, "{}");

        var found = ProjectFileDiscovery.FindProjectFiles(new[] { _baseDir });

        Assert.Contains(projektJson, found);
    }

    [Fact]
    public void FindProjectFiles_IgnoriertUnterordnerOhneProjektdatei()
    {
        Directory.CreateDirectory(Path.Combine(_baseDir, "NurFotos"));
        File.WriteAllText(Path.Combine(_baseDir, "NurFotos", "bild.jpg"), "x");

        var found = ProjectFileDiscovery.FindProjectFiles(new[] { _baseDir });

        Assert.Empty(found);
    }

    [Fact]
    public void FindProjectFiles_LiefertKeineDuplikate_BeiDoppeltenBasisordnern()
    {
        var projektDir = Directory.CreateDirectory(Path.Combine(_baseDir, "Zone 1.15")).FullName;
        var projektJson = Path.Combine(projektDir, "projekt.json");
        File.WriteAllText(projektJson, "{}");

        var found = ProjectFileDiscovery.FindProjectFiles(new[] { _baseDir, _baseDir.ToUpperInvariant() });

        Assert.Single(found, f => string.Equals(f, projektJson, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FindProjectFiles_WirftNicht_BeiNichtExistierendemBasisordner()
    {
        var found = ProjectFileDiscovery.FindProjectFiles(new[] { Path.Combine(_baseDir, "gibt_es_nicht") });

        Assert.Empty(found);
    }

    [Fact]
    public void FindProjectFiles_FindetBeideStrukturenGemischt()
    {
        // Realbild D:\Projekte: Alt-Projekte, neue Struktur und Nicht-Projekt-Ordner gemischt.
        var alt = Directory.CreateDirectory(Path.Combine(_baseDir, "Gosmergasse")).FullName;
        var altJson = Path.Combine(alt, "Gosmergasse.json");
        File.WriteAllText(altJson, "{}");

        var neu = Directory.CreateDirectory(Path.Combine(_baseDir, "Fuerlauwi", "Projektdateien")).FullName;
        var neuJson = Path.Combine(neu, "projekt.json");
        File.WriteAllText(neuJson, "{}");

        Directory.CreateDirectory(Path.Combine(_baseDir, "Verteilung"));

        var found = ProjectFileDiscovery.FindProjectFiles(new[] { _baseDir });

        Assert.Contains(altJson, found);
        Assert.Contains(neuJson, found);
        Assert.Equal(2, found.Count);
    }
}
