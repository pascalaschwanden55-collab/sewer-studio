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

    /// <summary>
    /// Nova-Fixwelle 2b (C1): Die Marke in der Kopfzeile der Uebersicht schreibt ihren Text mit
    /// demselben Konverter wie die Tabellenmarke. Mit <c>StringFormat=Z{0}</c> stand bei einer
    /// leeren oder unbekannten Klasse ein nacktes "Z" da — der Gedankenstrich ist der getrennte
    /// Zustand "nicht berechnet".
    /// </summary>
    [Fact]
    public void Die_Marke_der_Uebersicht_zeigt_ohne_Klasse_einen_Gedankenstrich()
    {
        var xaml = Xaml("Views", "Pages", "Haltungsansicht", "HaltungUebersichtPanel.xaml");
        Assert.Contains("ZustandsklasseChipTextConverter", xaml);
        Assert.DoesNotContain("StringFormat=Z{0}", xaml);
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

    /// <summary>
    /// Nova, Aufklapp-Liste (Task 4): In der Uebersicht steht die Haltungsgrafik des Protokolls
    /// (Spec-Ergaenzung Pascal 08.09.) statt des Rohrrings. Eckdaten und KI-Hinweis bleiben.
    /// </summary>
    [Fact]
    public void Uebersicht_zeigt_Haltungsgrafik_Fakten_und_KI_Hinweis()
    {
        var xaml = Xaml("Views", "Pages", "Haltungsansicht", "HaltungUebersichtPanel.xaml");
        foreach (var t in new[] { "local:HaltungsgrafikControl", "Schacht oben", "Schacht unten", "DN / Profil", "Prüfung", "Video", "Im Player prüfen", "KI-Vorschläge warten auf fachliche Bestätigung" })
            Assert.Contains(t, xaml);

        // Der Rohrring wird nicht mehr gebunden, bleibt aber samt Geometrie im Programm.
        Assert.DoesNotContain("local:RohrringControl", xaml);
        Assert.True(File.Exists(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "Haltungsansicht", "RohrringControl.xaml")));
        Assert.True(File.Exists(RepoFile("src", "AuswertungPro.Next.Application", "UseCases", "Uebersicht", "RohrringGeometrie.cs")));
    }

    /// <summary>
    /// Die Grafik braucht den aktiven Codekatalog fuer ihre Klartexte. Er kommt von der Seite
    /// ueber den Arbeitsflaechen-Controller — kein Dienstezugriff im Panel oder in der Seite.
    /// </summary>
    [Fact]
    public void Die_Haltungsgrafik_bekommt_ihren_Codekatalog_von_der_Seite()
    {
        var xaml = Xaml("Views", "Pages", "Haltungsansicht", "HaltungUebersichtPanel.xaml");
        Assert.Contains("Catalog=\"{Binding Catalog, ElementName=Root}\"", xaml);

        var controller = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "DataPage", "DataPageNovaWorkspaceController.cs"));
        Assert.Contains("public void VerbindeKatalog()", controller, StringComparison.Ordinal);
        Assert.Contains("_e.Uebersicht.Catalog = vm.CodeCatalog;", controller, StringComparison.Ordinal);

        var seite = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.NovaWorkspace.cs"));
        Assert.Contains("_novaWorkspace?.VerbindeKatalog();", seite, StringComparison.Ordinal);

        // Die Grafik selbst holt sich keinen Dienst.
        var control = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "Haltungsansicht", "HaltungsgrafikControl.xaml.cs"));
        Assert.DoesNotContain("App.Services", control, StringComparison.Ordinal);
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
    /// <summary>
    /// Nova, Aufklapp-Liste (Task 2): Die Liste steht in derselben Zelle wie die Tabelle und
    /// bindet dieselbe Ansicht der Datensaetze. Nur so wirken Suche und Filterzeile ohne
    /// zweiten Weg, und die Auswahl ueberlebt den Wechsel der Ansicht.
    /// </summary>
    [Fact]
    public void Die_Aufklapp_Liste_steht_in_derselben_Zelle_wie_die_Tabelle()
    {
        var xaml = Xaml("Views", "Pages", "DataPage.xaml");
        var liste = Regex.Match(xaml, @"<haltung:HaltungAufklappListe[\s\S]*?/>");
        Assert.True(liste.Success, "HaltungAufklappListe fehlt in DataPage.xaml");
        Assert.Contains("x:Name=\"AufklappListe\"", liste.Value);
        Assert.Contains("Grid.Row=\"1\"", liste.Value);
        Assert.Contains("Grid.Column=\"0\"", liste.Value);
        Assert.Contains("ItemsSource=\"{Binding Records}\"", liste.Value);
        Assert.Contains("SelectedItem=\"{Binding Selected, Mode=TwoWay}\"", liste.Value);
        // Video und Protokoll sind die vorhandenen Befehle der Seite, kein zweiter Weg.
        Assert.Contains("VideoCommand=\"{Binding PlayVideoCommand}\"", liste.Value);
        Assert.Contains("ProtokollCommand=\"{Binding OpenOriginalPdfCommand}\"", liste.Value);

        // Die Tabelle bindet dieselbe Ansicht — eine Sammlung, eine Auswahl.
        Assert.Contains("<DataGrid x:Name=\"Grid\"", xaml);
        Assert.Equal(2, Regex.Matches(xaml, @"SelectedItem=""\{Binding Selected, Mode=TwoWay\}""").Count);
    }

    /// <summary>
    /// Die drei Ansichten stehen als Gruppe im Menue "Weitere Aktionen". Genau einer der beiden
    /// Nova-Punkte ist angehakt; deaktivierte Gruppenkoepfe gibt es nicht (XamlActionWiringGuard).
    /// </summary>
    [Fact]
    public void Das_Menue_fuehrt_Aufklapp_Liste_Tabelle_und_die_alte_Ansicht()
    {
        var xaml = Xaml("Views", "Pages", "DataPage.xaml");

        foreach (var (name, header, tag) in new[]
                 {
                     ("AnsichtListeMenu", "Aufklapp-Liste", "liste"),
                     ("AnsichtTabelleMenu", "Tabelle", "tabelle")
                 })
        {
            var punkt = Regex.Match(xaml, @"<MenuItem x:Name=""" + name + @"""[\s\S]*?/>");
            Assert.True(punkt.Success, name + " fehlt");
            Assert.Contains("Header=\"" + header + "\"", punkt.Value);
            Assert.Contains("IsCheckable=\"True\"", punkt.Value);
            Assert.Contains("Tag=\"" + tag + "\"", punkt.Value);
            Assert.Contains("Click=\"AnsichtMenu_Click\"", punkt.Value);
        }

        Assert.Contains("Header=\"Alte Haltungsansicht\"", xaml);
        // Der Hinweis am gesperrten Abdocken muss sichtbar sein duerfen.
        Assert.Contains("ToolTipService.ShowOnDisabled=\"True\"", xaml);
    }

    /// <summary>
    /// Tabelle und Liste teilen sich EIN Zeilen-Kontextmenue. Zwei Wege zu denselben Aktionen
    /// hiessen, dass nur einer die Pruefungen bekommt (Re-Review 07.09.2026, Schachtseite).
    /// </summary>
    [Fact]
    public void Tabelle_und_Liste_teilen_sich_dasselbe_Zeilen_Kontextmenue()
    {
        var xaml = Xaml("Views", "Pages", "DataPage.xaml");
        Assert.Single(Regex.Matches(xaml, @"<ContextMenu x:Key=""HaltungZeilenMenue"">"));
        Assert.Contains("ContextMenu=\"{StaticResource HaltungZeilenMenue}\"", xaml);
        Assert.Contains("ZeilenMenue=\"{StaticResource HaltungZeilenMenue}\"", xaml);
        Assert.DoesNotContain("<DataGrid.ContextMenu>", xaml);

        // Die Liste reicht das Menue an ihre ListBox und waehlt bei Rechtsklick zuerst die Zeile.
        var listeXaml = Xaml("Views", "Pages", "Haltungsansicht", "HaltungAufklappListe.xaml");
        Assert.Contains("ContextMenu=\"{Binding ZeilenMenue, ElementName=Root}\"", listeXaml);
        Assert.Contains("PreviewMouseRightButtonDown=\"Liste_PreviewMouseRightButtonDown\"", listeXaml);
    }

    /// <summary>Die Aufklapp-Liste ist die Standardansicht der Haltungen.</summary>
    [Fact]
    public void Die_Aufklapp_Liste_ist_per_Einstellung_der_Standard()
    {
        var settings = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "AppSettings.cs"));
        Assert.Contains("public string HaltungenAnsicht { get; set; } = \"liste\";", settings);
    }

    /// <summary>
    /// Spaltenchips, Eingabefelder und Abdocken haengen an der Ansicht — und die Entscheidung
    /// darueber faellt an genau einer Stelle. Die Seite selbst darf sie nicht ein zweites Mal
    /// treffen (frueher: ApplyHaltungsansichtSichtbarkeit in DataPage.xaml.cs).
    /// </summary>
    [Fact]
    public void Chips_Eingabefelder_und_Abdocken_haengen_an_einer_einzigen_Ansichtsregel()
    {
        var umschalter = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "DataPage", "DataPageAnsichtUmschalter.cs"));
        Assert.Contains("HaltungenAnsichtRegel.Bestimme", umschalter);
        Assert.Contains("_e.Spaltenchips.Visibility", umschalter);
        Assert.Contains("_setzeArbeitsflaeche(sicht.Uebersicht, sicht.Eingabefelder)", umschalter);
        Assert.Contains("AbdockenNurTabelle", umschalter);

        var seite = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages");
        foreach (var datei in Directory.EnumerateFiles(seite, "DataPage*.cs"))
            Assert.DoesNotContain("ApplyHaltungsansichtSichtbarkeit", File.ReadAllText(datei));
    }
    /// <summary>
    /// Fix-Runde 1 (3): In der Aufklapp-Liste steht das Formular in der aufgeklappten Zeile. Die
    /// Eingabefelder-Schublade wird dann nicht nur ausgeblendet, sondern geleert UND ihr
    /// Live-Abgleich entsorgt — sonst haengen zwei Formulare am selben Datensatz und der
    /// Konflikthinweis landet im unsichtbaren.
    /// </summary>
    [Fact]
    public void Das_unsichtbare_Formular_wird_entsorgt_nicht_nur_ausgeblendet()
    {
        var controller = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "DataPage", "DataPageNovaWorkspaceController.cs"));
        var methode = Regex.Match(controller, @"public void LeereFelderDrawer\(\)[\s\S]*?
    \}");
        Assert.True(methode.Success, "LeereFelderDrawer fehlt");
        Assert.Contains("_felderSync?.Dispose();", methode.Value, StringComparison.Ordinal);
        Assert.Contains("_felderSync = null;", methode.Value, StringComparison.Ordinal);
        Assert.Contains("Groups = null;", methode.Value, StringComparison.Ordinal);

        var seite = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.NovaWorkspace.cs"));
        Assert.Contains("_ansicht?.ListeSichtbar == true", seite, StringComparison.Ordinal);
        Assert.Contains("LeereFelderDrawer()", seite, StringComparison.Ordinal);

        // Die Weiche fuer den Konflikthinweis liegt im Umschalter, nicht in der Seite.
        Assert.Contains("_ansicht?.MeldeKonflikt(", seite, StringComparison.Ordinal);
    }

    /// <summary>
    /// Fix-Runde 1 (5): Das geteilte Zeilenmenue haengt an zwei verschiedenen Elementen. Ein
    /// <c>{Binding …}</c> ohne <c>PlacementTarget</c> zeigt deshalb je nach Ansicht woanders hin —
    /// die Punkte duerfen ihre Haltung nur ueber das PlacementTarget oder ueber Click finden.
    /// </summary>
    [Fact]
    public void Das_geteilte_Zeilenmenue_bindet_nur_ueber_PlacementTarget()
    {
        var xaml = Xaml("Views", "Pages", "DataPage.xaml");
        var menue = Regex.Match(xaml, @"<ContextMenu x:Key=""HaltungZeilenMenue"">[\s\S]*?</ContextMenu>");
        Assert.True(menue.Success, "Das geteilte Zeilenmenue fehlt");

        foreach (Match bindung in Regex.Matches(menue.Value, @"\{Binding [^}]*\}"))
        {
            // DynamicResource-Farben und Click-Punkte sind davon nicht betroffen.
            Assert.Contains("PlacementTarget", bindung.Value, StringComparison.Ordinal);
        }

        // Der Loeschpunkt traegt seine Marke, damit der Umschalter ihn ohne x:Name findet
        // (in einem Ressourcenteil gibt es keinen Namescope der Seite).
        Assert.Contains("Tag=\"loeschen\"", menue.Value, StringComparison.Ordinal);
    }
    /// <summary>
    /// Fix-Runde 2 (1): Das Abo des Sprungs von aussen muss symmetrisch sein. <c>Unloaded</c>
    /// meldete ab, <c>Loaded</c> nicht wieder an — nach einem Unload/Load derselben Seite blieb
    /// der Sprung aus Dossier, Karte und Suche tot, und das faellt nicht auf, weil die Haltung
    /// trotzdem ausgewaehlt wird und nur das Aufklappen fehlt.
    ///
    /// Der Weg laeuft ueber genau eine Stelle (<c>VerbindeAnzeigeAuftrag</c>), die immer zuerst
    /// abmeldet und deshalb mehrfach sicher ist.
    /// </summary>
    [Fact]
    public void Das_Abo_des_Sprungs_von_aussen_ist_symmetrisch()
    {
        var seite = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var geladen = Regex.Match(seite, @"Loaded \+= \(_, __\) =>\s*\{[\s\S]*?
        \};");
        var entladen = Regex.Match(seite, @"Unloaded \+= \(_, __\) =>\s*\{[\s\S]*?
        \};");
        Assert.True(geladen.Success && entladen.Success, "Loaded/Unloaded der Seite nicht gefunden");
        Assert.Contains("VerbindeAnzeigeAuftrag(true)", geladen.Value, StringComparison.Ordinal);
        Assert.Contains("VerbindeAnzeigeAuftrag(false)", entladen.Value, StringComparison.Ordinal);
        // Der Controller der Liste wird beim Entladen entsorgt und beim Laden neu verdrahtet.
        Assert.Contains("_aufklappListe?.Dispose()", entladen.Value, StringComparison.Ordinal);
        Assert.Contains("_aufklappListe?.Verdrahte()", geladen.Value, StringComparison.Ordinal);

        var anbindung = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.AufklappListe.cs"));
        var methode = Regex.Match(anbindung, @"private void VerbindeAnzeigeAuftrag\(bool an\)[\s\S]*?
    \}");
        Assert.True(methode.Success, "VerbindeAnzeigeAuftrag fehlt");
        Assert.Contains("HaltungAnzeigen -= ZeigeHaltungInListe;", methode.Value, StringComparison.Ordinal);
        Assert.Contains("HaltungAnzeigen += ZeigeHaltungInListe;", methode.Value, StringComparison.Ordinal);
        // Erst abmelden, dann anmelden: sonst haengt der Sprung nach zwei Laeufen doppelt.
        Assert.True(
            methode.Value.IndexOf("-= ZeigeHaltungInListe", StringComparison.Ordinal)
            < methode.Value.IndexOf("+= ZeigeHaltungInListe", StringComparison.Ordinal),
            "VerbindeAnzeigeAuftrag muss zuerst abmelden.");
    }

    /// <summary>
    /// Fix-Runde 2 (2): <c>Waehle</c> wendet die Ansicht selbst an. Ein zweiter
    /// <c>WendeAnsichtAn()</c> im Menue-Handler waere derselbe Lauf ein zweites Mal — samt einem
    /// zweiten <c>NimmAnzeigeAuftrag</c>.
    /// </summary>
    [Fact]
    public void Der_Menue_Handler_wendet_die_Ansicht_nicht_zweimal_an()
    {
        var anbindung = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.AufklappListe.cs"));
        var handler = Regex.Match(anbindung, @"private void AnsichtMenu_Click\([\s\S]*?
    \}");
        Assert.True(handler.Success, "AnsichtMenu_Click fehlt");
        Assert.Contains("_ansicht?.Waehle(sender)", handler.Value, StringComparison.Ordinal);
        Assert.Contains("AktualisiereFormulare()", handler.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("WendeAnsichtAn()", handler.Value, StringComparison.Ordinal);

        var umschalter = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "DataPage", "DataPageAnsichtUmschalter.cs"));
        var waehle = Regex.Match(umschalter, @"public void Waehle\(string\? ansicht\)[\s\S]*?
    \}");
        Assert.True(waehle.Success, "Waehle(string) fehlt");
        Assert.Contains("WendeAn();", waehle.Value, StringComparison.Ordinal);
    }
}
