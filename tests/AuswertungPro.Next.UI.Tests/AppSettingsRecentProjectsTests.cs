using System.Linq;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Charakterisierung der Projekt-Merkliste (RecentProjectPaths) — sie ist das
/// Gedaechtnis der Projektuebersicht und muss bei jedem Oeffnen gepflegt werden.
/// </summary>
public sealed class AppSettingsRecentProjectsTests
{
    [Fact]
    public void AddRecentProject_SetztAuchLastProjectPath()
    {
        var settings = new AppSettings();

        settings.AddRecentProject(@"D:\Projekte\Zone 1.15\Altdorf.json");

        Assert.Equal(@"D:\Projekte\Zone 1.15\Altdorf.json", settings.LastProjectPath);
    }

    [Fact]
    public void AddRecentProject_NeuesterZuerst_OhneDuplikate()
    {
        var settings = new AppSettings();

        settings.AddRecentProject(@"D:\Projekte\A\projekt.json");
        settings.AddRecentProject(@"D:\Projekte\B\projekt.json");
        settings.AddRecentProject(@"d:\projekte\a\PROJEKT.json"); // Duplikat, andere Schreibweise

        Assert.Equal(2, settings.RecentProjectPaths.Count);
        Assert.Equal(@"d:\projekte\a\PROJEKT.json", settings.RecentProjectPaths[0]);
        Assert.Equal(@"D:\Projekte\B\projekt.json", settings.RecentProjectPaths[1]);
    }

    [Fact]
    public void AddRecentProject_BegrenztAufZwanzigEintraege()
    {
        var settings = new AppSettings();

        for (var i = 0; i < 25; i++)
            settings.AddRecentProject($@"D:\Projekte\P{i}\projekt.json");

        Assert.Equal(20, settings.RecentProjectPaths.Count);
        Assert.Equal(@"D:\Projekte\P24\projekt.json", settings.RecentProjectPaths[0]);
    }

    [Fact]
    public void AddRecentProject_IgnoriertLeerePfade()
    {
        var settings = new AppSettings();

        settings.AddRecentProject("");
        settings.AddRecentProject("   ");

        Assert.Empty(settings.RecentProjectPaths);
    }
}
