using System.IO;
using System.Linq;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// ResolveAll liefert alle Basisordner fuer den Projektlisten-Scan — inklusive
/// der aus dem letzten Projekt / der Merkliste GELERNTEN Wurzeln. Hintergrund:
/// Der alte Fallback suchte nur in D:\Projekt, die echten Projekte lagen aber
/// in D:\Projekte — die Liste blieb leer, sobald die Settings-Merkliste weg war.
/// </summary>
public sealed class ProjectScanRootsResolveAllTests
{
    [Fact]
    public void ResolveAll_LerntWurzelAusLetztemProjekt_AltStruktur()
    {
        // Alt: D:\Projekte\Zone 1.15\Altdorf.json -> Wurzel D:\Projekte
        var roots = ProjectScanRoots.ResolveAll(
            @"C:\App", null,
            lastProjectPath: @"D:\Projekte\Zone 1.15\Altdorf.json",
            recentProjectPaths: null);

        Assert.Contains(@"D:\Projekte", roots);
    }

    [Fact]
    public void ResolveAll_LerntWurzelAusLetztemProjekt_NeueStruktur()
    {
        // Neu: D:\Projekte\Fuerlauwi\Projektdateien\projekt.json -> Wurzel D:\Projekte
        var roots = ProjectScanRoots.ResolveAll(
            @"C:\App", null,
            lastProjectPath: @"D:\Projekte\Fuerlauwi\Projektdateien\projekt.json",
            recentProjectPaths: null);

        Assert.Contains(@"D:\Projekte", roots);
    }

    [Fact]
    public void ResolveAll_LerntWurzelnAusMerkliste()
    {
        var roots = ProjectScanRoots.ResolveAll(
            @"C:\App", null,
            lastProjectPath: null,
            recentProjectPaths: new[] { @"E:\Archiv\Unterdorf\projekt.json" });

        Assert.Contains(@"E:\Archiv", roots);
    }

    [Fact]
    public void ResolveAll_EnthaeltStandardFallbacks_MitUndOhneE()
    {
        var roots = ProjectScanRoots.ResolveAll(@"C:\App", null, null, null);

        Assert.Contains(@"D:\Projekt", roots);
        Assert.Contains(@"D:\Projekte", roots);
        Assert.Contains(@"C:\Projekt", roots);
        Assert.Contains(@"C:\Projekte", roots);
    }

    [Fact]
    public void ResolveAll_EnthaeltKonfiguriertesVerzeichnisUndRohdaten()
    {
        var roots = ProjectScanRoots.ResolveAll(@"C:\App", @"E:\MeineProjekte", null, null);

        Assert.Contains(@"E:\MeineProjekte", roots);
        Assert.Contains(Path.Combine(@"C:\App", "Rohdaten"), roots);
    }

    [Fact]
    public void ResolveAll_LiefertKeineDuplikate()
    {
        var roots = ProjectScanRoots.ResolveAll(
            @"C:\App", @"D:\Projekte",
            lastProjectPath: @"D:\Projekte\Zone 1.15\Altdorf.json",
            recentProjectPaths: new[] { @"d:\projekte\Gosmergasse\Gosmergasse.json" });

        var distinct = roots.Distinct(System.StringComparer.OrdinalIgnoreCase).Count();
        Assert.Equal(distinct, roots.Count);
    }

    [Fact]
    public void ResolveAll_IgnoriertLeerePfade()
    {
        var roots = ProjectScanRoots.ResolveAll(
            @"C:\App", "   ",
            lastProjectPath: "",
            recentProjectPaths: new[] { "", "   " });

        Assert.DoesNotContain(roots, string.IsNullOrWhiteSpace);
    }
}
