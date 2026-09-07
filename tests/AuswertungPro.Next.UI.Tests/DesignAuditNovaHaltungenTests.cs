using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Nova-Etappe 1: Arbeitsflaeche der Haltungen-Seite (Liste, Uebersicht rechts, Eingabefelder unten).</summary>
public sealed class DesignAuditNovaHaltungenTests
{
    private static string Xaml(params string[] parts)
        => File.ReadAllText(RepoFile(new[] { "src", "AuswertungPro.Next.UI" }.Concat(parts).ToArray()));

    [Fact]
    public void Haltungen_hat_Uebersicht_rechts_und_Eingabefelder_unten_mit_gespeicherten_Trennlinien()
    {
        var xaml = Xaml("Views", "Pages", "DataPage.xaml");
        Assert.Contains("HaltungUebersichtPanel", xaml);
        Assert.Contains("HaltungFelderDrawer", xaml);
        Assert.Contains("SplitterKey=\"HaltungenUebersicht\"", xaml);
        Assert.Contains("SplitterKey=\"HaltungenEingabefelder\"", xaml);
        // Die Splitter-Persistenz braucht einen vererbten ViewKey am Container.
        Assert.Contains("ViewPersonalization.ViewKey=\"DataPage\"", xaml);
        // Die alte Ansicht bleibt erreichbar.
        Assert.Contains("x:Name=\"HaltungsansichtToggle\"", xaml);
    }

    [Fact]
    public void Eingabefelder_zeigen_die_Themen_ueber_RecordDetailsView()
    {
        var xaml = Xaml("Views", "Pages", "Haltungsansicht", "HaltungFelderDrawer.xaml");
        Assert.Contains("controls:RecordDetailsView", xaml);
        Assert.Contains("<Expander", xaml);
        Assert.Contains("AutomationProperties.Name=\"Feld suchen\"", xaml);
    }

    [Fact]
    public void Uebersicht_verwendet_die_Zustandsklassen_Tinte()
    {
        var xaml = Xaml("Views", "Pages", "Haltungsansicht", "HaltungUebersichtPanel.xaml");
        Assert.Contains("ZustandsklasseInkConverter", xaml);
        Assert.DoesNotContain("Foreground=\"White\"", xaml);
    }

    [Fact]
    public void Nova_Arbeitsflaeche_ist_per_Einstellung_der_Standard()
    {
        var settings = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "AppSettings.cs"));
        Assert.Contains("public bool ShowHaltungenNovaLayout { get; set; } = true;", settings);
    }

    [Fact]
    public void Der_Umschalter_zur_alten_Haltungsansicht_liegt_im_Menue_und_nicht_in_der_Werkzeugleiste()
    {
        var xaml = Xaml("Views", "Pages", "DataPage.xaml");
        var toggle = Regex.Match(xaml, "<MenuItem x:Name=\"HaltungsansichtToggle\"[\\s\\S]*?/>|<MenuItem x:Name=\"HaltungsansichtToggle\"[\\s\\S]*?</MenuItem>");
        Assert.True(toggle.Success, "HaltungsansichtToggle muss ein MenuItem sein");
        Assert.Contains("IsCheckable=\"True\"", toggle.Value);
        Assert.Contains("Header=\"Alte Haltungsansicht\"", toggle.Value);
        Assert.DoesNotContain("<ToggleButton x:Name=\"HaltungsansichtToggle\"", xaml);
    }

    [Fact]
    public void Eingabefelder_haben_Zaehler_je_Thema_und_einen_Knopf_gross_anzeigen()
    {
        var xaml = Xaml("Views", "Pages", "Haltungsansicht", "HaltungFelderDrawer.xaml");
        Assert.Contains("{Binding Anzahl}", xaml);
        Assert.Contains("AutomationProperties.Name=\"Eingabefelder gross anzeigen\"", xaml);
    }

    [Fact]
    public void Uebersicht_zeigt_Rohrring_Fakten_und_KI_Hinweis()
    {
        var xaml = Xaml("Views", "Pages", "Haltungsansicht", "HaltungUebersichtPanel.xaml");
        foreach (var t in new[] { "local:RohrringControl", "Schacht oben", "Schacht unten", "DN / Profil", "Prüfung", "Video", "Im Player prüfen", "KI-Vorschläge warten auf fachliche Bestätigung" })
            Assert.Contains(t, xaml);
    }

    /// <summary>
    /// Nova-Etappe 2b, Task 4: Die Suche steht als Pille rechts, gleiches Muster wie die
    /// globale Suche in MainWindow.xaml (RadiusPill, InputBorderBrush, Lupe, Tastenmarke F3).
    /// Die alte Ansicht (ShowHaltungenNovaLayout=false) behaelt ihre eigene, unveraenderte Zeile.
    /// </summary>
    [Fact]
    public void Werkzeugleiste_zeigt_die_Suche_als_Pille_mit_F3_Marke()
    {
        var xaml = Xaml("Views", "Pages", "DataPage.xaml");
        Assert.Contains("x:Name=\"NovaSucheLeiste\"", xaml);
        Assert.Contains("x:Name=\"AlteSucheLeiste\"", xaml);
        Assert.Contains("{DynamicResource RadiusPill}", xaml);
        Assert.Contains("{DynamicResource InputBorderBrush}", xaml);
        Assert.Contains("Text=\"F3\"", xaml);
        Assert.Contains("Suche Haltung", xaml);
        // Die alte Zeile bleibt textlich unveraendert erreichbar (Beschriftung + Feld).
        Assert.Contains("Text=\"Suche Haltung:\"", xaml);
    }

    /// <summary>
    /// Nova-Etappe 2b, Task 4: "Verschieben auf Pos." und "Gehe zu Zeile" stehen nicht mehr
    /// staendig in der Werkzeugleiste, sondern nur noch im Popup "Reihenfolge" unter
    /// "Weitere Aktionen". Ein MenuItem "Reihenfolge" oeffnet dieses Popup.
    /// </summary>
    [Fact]
    public void Verschieben_und_GeheZuZeile_liegen_nur_noch_im_Popup_Reihenfolge()
    {
        var xaml = Xaml("Views", "Pages", "DataPage.xaml");
        Assert.Contains("Header=\"Reihenfolge\"", xaml);
        Assert.Contains("Click=\"ReihenfolgeMenu_Click\"", xaml);

        var popup = Regex.Match(xaml, "<Popup x:Name=\"ReihenfolgePopup\"[\\s\\S]*?</Popup>");
        Assert.True(popup.Success, "ReihenfolgePopup nicht gefunden");
        Assert.Contains("Verschieben auf Pos.:", popup.Value);
        Assert.Contains("Gehe zu Zeile:", popup.Value);
        Assert.Contains("x:Name=\"MoveToPositionBox\"", popup.Value);
        Assert.Contains("x:Name=\"GoToRowBox\"", popup.Value);

        var ausserhalbDesPopups = xaml.Remove(popup.Index, popup.Length);
        Assert.DoesNotContain("Verschieben auf Pos.:", ausserhalbDesPopups);
        Assert.DoesNotContain("Gehe zu Zeile:", ausserhalbDesPopups);
    }

    /// <summary>F3 fokussiert die Suche auf Seitenebene (PreviewKeyDown), nicht ueber ein KeyBinding im Menue.</summary>
    [Fact]
    public void F3_fokussiert_die_Suche()
    {
        var xaml = Xaml("Views", "Pages", "DataPage.xaml");
        Assert.Contains("PreviewKeyDown=\"DataPage_PreviewKeyDown\"", xaml);

        var code = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.NovaSucheUndReihenfolge.cs"));
        Assert.Contains("Key.F3", code);
        Assert.Contains("NovaSearchBox", code);
    }

    /// <summary>
    /// Fix-Runde 1: Das Popup "Reihenfolge" setzt beim Oeffnen den Fokus ins erste Feld
    /// (Popup.Opened) und schliesst bei Escape wieder mit Fokus zurueck an den Menueknopf.
    /// </summary>
    [Fact]
    public void Reihenfolge_Popup_fokussiert_beim_Oeffnen_und_schliesst_bei_Escape()
    {
        var xaml = Xaml("Views", "Pages", "DataPage.xaml");
        Assert.Contains("Opened=\"ReihenfolgePopup_Opened\"", xaml);
        Assert.Contains("PreviewKeyDown=\"ReihenfolgePopup_PreviewKeyDown\"", xaml);

        var code = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.NovaSucheUndReihenfolge.cs"));
        Assert.Contains("PopupFocusHelper.FokussiereErstesFeld(MoveToPositionBox)", code);
        Assert.Contains("PopupFocusHelper.SchliesseBeiEscape(e.Key, ReihenfolgePopup, WeitereAktionenDropdown)", code);
    }

    /// <summary>
    /// Nova-Etappe 2b, Task 6: Ohne gewaehlte Zeile zeigt die Uebersicht NUR den Leerzustand.
    /// Vorher standen Rohrring, alle Beschriftungen und leere Werte da — und weil eine
    /// Feldbindung ohne Datensatz DependencyProperty.UnsetValue liefert, bei DN / Profil sogar
    /// der Fehltext "{DependencyProperty.UnsetValue}" (Pascals Bild vom 07.09.).
    /// </summary>
    [Fact]
    public void Uebersicht_zeigt_ohne_Auswahl_nur_den_Leerzustand()
    {
        var xaml = Xaml("Views", "Pages", "Haltungsansicht", "HaltungUebersichtPanel.xaml");
        Assert.Contains("x:Name=\"Leerzustand\"", xaml);
        Assert.Contains("Keine Haltung gewählt. Links eine Zeile wählen.", xaml);

        // Der ganze Inhalt haengt an einem einzigen Sichtbarkeitsschalter: Record == null.
        var inhalt = Regex.Match(xaml, @"<ScrollViewer x:Name=""Inhalt""[\s\S]*?</ScrollViewer.Style>");
        Assert.True(inhalt.Success, "Inhalt der Uebersicht braucht einen eigenen Sichtbarkeitsschalter");
        Assert.Contains("<DataTrigger Binding=\"{Binding Record, ElementName=Root}\" Value=\"{x:Null}\">", inhalt.Value);
        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Collapsed\"/>", inhalt.Value);
    }
}
