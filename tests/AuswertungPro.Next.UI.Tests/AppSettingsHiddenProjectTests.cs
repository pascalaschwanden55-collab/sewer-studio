using AuswertungPro.Next.UI;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// "Loeschen" in der Projektuebersicht blendet ein Projekt nur aus der Liste aus —
/// die Projektdatei und alle Daten im Ordner bleiben erhalten.
/// </summary>
public sealed class AppSettingsHiddenProjectTests
{
    private const string ProjectPath = @"D:\Projekte\Zone 1.15\Projektdateien\projekt.json";

    [Fact]
    public void HideProject_nimmt_aus_merkliste_und_markiert_als_ausgeblendet()
    {
        var settings = new AppSettings();
        settings.AddRecentProject(ProjectPath);

        settings.HideProject(ProjectPath);

        Assert.DoesNotContain(ProjectPath, settings.RecentProjectPaths);
        Assert.Contains(ProjectPath, settings.HiddenProjectPaths);
        Assert.Null(settings.LastProjectPath);
    }

    [Fact]
    public void Erneutes_oeffnen_macht_projekt_wieder_sichtbar()
    {
        var settings = new AppSettings();
        settings.AddRecentProject(ProjectPath);
        settings.HideProject(ProjectPath);

        settings.AddRecentProject(ProjectPath);

        Assert.DoesNotContain(ProjectPath, settings.HiddenProjectPaths);
        Assert.Contains(ProjectPath, settings.RecentProjectPaths);
    }

    [Fact]
    public void HideProject_legt_keine_duplikate_an()
    {
        var settings = new AppSettings();

        settings.HideProject(ProjectPath);
        settings.HideProject(ProjectPath);

        Assert.Single(settings.HiddenProjectPaths);
    }

    [Fact]
    public void HideProject_ignoriert_leere_pfade()
    {
        var settings = new AppSettings();

        settings.HideProject("");
        settings.HideProject("   ");

        Assert.Empty(settings.HiddenProjectPaths);
    }
}
