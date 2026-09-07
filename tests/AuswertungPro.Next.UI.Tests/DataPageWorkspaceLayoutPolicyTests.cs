using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Nova-Etappe 1: Hoehe der Eingabefelder unter der Haltungsliste.</summary>
public sealed class DataPageWorkspaceLayoutPolicyTests
{
    // Grosser Bildschirm: Arbeitsflaeche 556 px, Zeile 32 px, Tabellenkopf 32 px.
    [Fact]
    public void Standard_laesst_mindestens_sieben_Zeilen_sichtbar()
    {
        var layout = DataPageWorkspaceLayoutPolicy.Berechne(gesamtHoehe: 556, zeilenHoehe: 32, kopfHoehe: 32, gespeichert: null);
        Assert.False(layout.Zugeklappt);
        Assert.True(556 - layout.Hoehe - DataPageWorkspaceLayoutPolicy.SplitterHoehe >= 32 + 7 * 32, $"Eingabefelder {layout.Hoehe} px");
        Assert.True(layout.Hoehe >= DataPageWorkspaceLayoutPolicy.MinDrawer);
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

    // 1366 x 768 am Programm (Sichtprobe 06.09.2026): rund 400 px fuer Liste und Eingabefelder,
    // Zeile 38 px, Tabellenkopf samt Bildlaufleiste 54 px. Sieben Zeilen brauchen 320 px,
    // dazu Mindesthoehe 120 plus Trennlinie: passt nicht -> Eingabefelder zuklappen.
    [Fact]
    public void Zu_wenig_Flaeche_klappt_die_Eingabefelder_zu_damit_sieben_Zeilen_bleiben()
    {
        var layout = DataPageWorkspaceLayoutPolicy.Berechne(gesamtHoehe: 400, zeilenHoehe: 38, kopfHoehe: 54, gespeichert: 220);
        Assert.True(layout.Zugeklappt);
        Assert.Equal(DataPageWorkspaceLayoutPolicy.MinDrawer, layout.Hoehe);
    }

    [Fact]
    public void Knapp_ausreichende_Flaeche_bleibt_aufgeklappt_mit_Mindesthoehe()
    {
        // 320 (Liste) + 6 + 120 = 446 -> bei 450 px reicht es gerade.
        var layout = DataPageWorkspaceLayoutPolicy.Berechne(gesamtHoehe: 450, zeilenHoehe: 38, kopfHoehe: 54, gespeichert: 300);
        Assert.False(layout.Zugeklappt);
        Assert.Equal(124, layout.Hoehe);
    }

    // "Gross anzeigen" (Fix-Runde 1, Inventar 4.3/8.6): die 60-Prozent-Wunschhoehe laeuft durch
    // dieselbe Regel wie jede gespeicherte Hoehe und wird deshalb ebenso auf sieben Zeilen
    // geklemmt beziehungsweise zugeklappt, wenn selbst die Mindesthoehe nicht mehr passt.
    [Fact]
    public void Grosse_Anzeige_klappt_zu_wenn_selbst_die_Mindesthoehe_nicht_mehr_passt()
    {
        // fuerListe = 54 + 7*32 = 278; maxDrawer = 400 - 278 - 6 = 116 < MinDrawer 120.
        var layout = DataPageWorkspaceLayoutPolicy.Berechne(gesamtHoehe: 400, zeilenHoehe: 32, kopfHoehe: 54, gespeichert: 240);
        Assert.True(layout.Zugeklappt);
        Assert.Equal(DataPageWorkspaceLayoutPolicy.MinDrawer, layout.Hoehe);
    }

    [Fact]
    public void Grosse_Anzeige_bleibt_bei_reichlich_Flaeche_bei_der_60_Prozent_Wunschhoehe()
    {
        // fuerListe = 54 + 7*32 = 278; maxDrawer = 900 - 278 - 6 = 616 >= 540.
        var layout = DataPageWorkspaceLayoutPolicy.Berechne(gesamtHoehe: 900, zeilenHoehe: 32, kopfHoehe: 54, gespeichert: 540);
        Assert.False(layout.Zugeklappt);
        Assert.Equal(540, layout.Hoehe);
    }
}
