using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Nova-Etappe 1: Hoehe der Eingabefelder unter der Haltungsliste.</summary>
public sealed class DataPageWorkspaceLayoutPolicyTests
{
    // 1366 x 768: Arbeitsflaeche rund 556 px hoch, Zeile 32 px, Tabellenkopf 32 px.
    [Fact]
    public void Standard_laesst_mindestens_sieben_Zeilen_sichtbar()
    {
        var drawer = DataPageWorkspaceLayoutPolicy.DrawerHeight(gesamtHoehe: 556, zeilenHoehe: 32, kopfHoehe: 32, gespeichert: null);
        Assert.True(556 - drawer - DataPageWorkspaceLayoutPolicy.SplitterHoehe >= 32 + 7 * 32, $"Eingabefelder {drawer} px");
        Assert.True(drawer >= DataPageWorkspaceLayoutPolicy.MinDrawer);
    }

    [Fact]
    public void Gespeicherte_Hoehe_wird_uebernommen_aber_auf_sieben_Zeilen_begrenzt()
    {
        Assert.Equal(180, DataPageWorkspaceLayoutPolicy.DrawerHeight(900, 32, 32, gespeichert: 180));
        var begrenzt = DataPageWorkspaceLayoutPolicy.DrawerHeight(556, 32, 32, gespeichert: 480);
        Assert.True(556 - begrenzt - DataPageWorkspaceLayoutPolicy.SplitterHoehe >= 32 + 7 * 32);
    }

    [Fact]
    public void Sehr_kleine_Flaeche_gibt_die_Mindesthoehe_zurueck()
        => Assert.Equal(DataPageWorkspaceLayoutPolicy.MinDrawer, DataPageWorkspaceLayoutPolicy.DrawerHeight(200, 32, 32, null));
}
