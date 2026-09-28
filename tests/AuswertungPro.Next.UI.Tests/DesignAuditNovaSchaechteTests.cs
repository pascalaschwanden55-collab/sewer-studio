using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditNovaSchaechteTests
{
    private static string Xaml() => File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechtePage.xaml"));

    [Fact]
    public void Werkzeugleiste_zeigt_nur_Hauptaktionen_und_ein_Menue_Weitere_Aktionen()
    {
        var xaml = Xaml();
        Assert.Contains("x:Name=\"WeitereAktionenDropdown\"", xaml);
        // Optikanalyse 28.09.2026, Aufgabe 7: unklare Namen sprechend gemacht ("PDF-Daten" ->
        // "Stammdaten aus PDFs ergänzen", "Aktualisieren" -> "Protokoll neu einlesen", "Strassen"
        // -> "Strassennamen ergänzen"); "Hoch"/"Runter" heissen jetzt wie bei den Haltungen
        // "Nach oben"/"Nach unten" (Reihenfolge-Untermenue).
        foreach (var header in new[] { "Stammdaten aus PDFs ergänzen", "Protokoll neu einlesen", "Leere Felder aus QGIS", "GeoShop-Abgleich (XTF)", "Feldnamen aufräumen", "Strassennamen ergänzen", "Nach oben", "Nach unten", "Ansicht anpassen" })
            Assert.Contains($"Header=\"{header}\"", xaml);
        // Aufgabe 8: "Alte Schachtansicht" ist aus dem Menü entfernt (Einstellungen -> Allgemein
        // -> Frühere Ansichten); das MenuItem bleibt nur unsichtbar als Zustandshalter stehen.
        Assert.DoesNotContain("Header=\"Alte Schachtansicht\"", xaml);
        Assert.DoesNotContain("<ToggleButton x:Name=\"SchachtansichtToggle\"", xaml);
        Assert.Contains("x:Name=\"ColumnViewChips\"", xaml);
        Assert.Contains("SchaechteColumnViewCatalog.Views", xaml);
    }

    /// <summary>
    /// Optikanalyse 28.09.2026, Aufgabe 7 (Fix-Runde 1, Controller-Entscheid "Auffindbarkeit
    /// schlaegt Entdoppelung"): "Weitere Aktionen" ist in denselben fuenf Untermenues gegliedert
    /// wie bei den Haltungen (Daten abgleichen, Bearbeiten, Reihenfolge, Ansicht, Ausgabe - siehe
    /// <see cref="WeitereAktionenUntermenueTests"/>). "Sanierungsmassnahmen...", "Protokoll
    /// (PDF)..." und "Gehe zu Ordner" stehen bewusst SOWOHL im Zeilenmenue (SchachtZeilenMenue)
    /// ALS AUCH hier unter "Bearbeiten"/"Ausgabe" - derselbe Handler, zwei Wege zur selben
    /// Aktion, damit sie auch ohne Rechtsklick auffindbar ist.
    /// </summary>
    [Fact]
    public void Gruppen_im_Menue_trennen_nur_mit_Separator_ohne_deaktivierte_Kopfzeilen()
    {
        var xaml = Xaml();
        Assert.DoesNotContain("IsEnabled=\"False\" Focusable=\"False\"", xaml);
        // Zwischen Separator und Menuepunkt darf ein erklaerender XML-Kommentar stehen, sonst
        // nur Leerraum - kein weiterer Menuepunkt dazwischen.
        const string kommentar = "(?:\\s*<!--[\\s\\S]*?-->)?\\s*";
        Assert.Matches(new Regex("<MenuItem Header=\"Daten abgleichen\""), xaml);
        Assert.Matches(new Regex("<Separator/>" + kommentar + "<MenuItem Header=\"Bearbeiten\""), xaml);
        Assert.Matches(new Regex("<Separator/>" + kommentar + "<MenuItem Header=\"Reihenfolge\""), xaml);
        Assert.Matches(new Regex("<Separator/>" + kommentar + "<MenuItem Header=\"Ansicht\""), xaml);
        Assert.Matches(new Regex("<Separator/>" + kommentar + "<MenuItem Header=\"Ausgabe\""), xaml);
        Assert.Matches(new Regex("<Separator/>" + kommentar + "<MenuItem Header=\"Ansicht anpassen\""), xaml);

        // Dieselben drei Eintraege stehen bewusst zusaetzlich im Dropdown-Block (Auffindbarkeit) ...
        var dropdownMenu = ExtractWeitereAktionenMenu(xaml);
        Assert.Contains("Header=\"Sanierungsmassnahmen...\" Click=\"SanierungsmassnahmenMenu_Click\"", dropdownMenu);
        Assert.Contains("Header=\"Protokoll (PDF)...\" Click=\"ProtokollMenu_Click\"", dropdownMenu);
        Assert.Contains("Header=\"Gehe zu Ordner\" Click=\"OpenContainingFolderMenu_Click\"", dropdownMenu);
        // ... und unveraendert im eigenen Zeilenmenue (SchachtZeilenMenue).
        var zeilenMenue = ExtractZeilenMenue(xaml);
        Assert.Contains("Header=\"Sanierungsmassnahmen...\" Click=\"SanierungsmassnahmenMenu_Click\"", zeilenMenue);
        Assert.Contains("Header=\"Protokoll (PDF)...\" Click=\"ProtokollMenu_Click\"", zeilenMenue);
        Assert.Contains("Header=\"Gehe zu Ordner\" Click=\"OpenContainingFolderMenu_Click\"", zeilenMenue);
    }

    private static string ExtractWeitereAktionenMenu(string xaml)
    {
        var start = xaml.IndexOf("x:Name=\"WeitereAktionenDropdown\"", System.StringComparison.Ordinal);
        Assert.True(start >= 0, "WeitereAktionenDropdown nicht gefunden");
        var contextStart = xaml.IndexOf("<Button.ContextMenu>", start, System.StringComparison.Ordinal);
        var contextEnd = xaml.IndexOf("</Button.ContextMenu>", contextStart, System.StringComparison.Ordinal);
        Assert.True(contextStart >= 0 && contextEnd > contextStart, "Button.ContextMenu von WeitereAktionenDropdown nicht gefunden");
        return xaml[contextStart..contextEnd];
    }

    private static string ExtractZeilenMenue(string xaml)
    {
        var start = xaml.IndexOf("<ContextMenu x:Key=\"SchachtZeilenMenue\">", System.StringComparison.Ordinal);
        Assert.True(start >= 0, "SchachtZeilenMenue nicht gefunden");
        var end = xaml.IndexOf("</ContextMenu>", start, System.StringComparison.Ordinal);
        Assert.True(end > start, "Ende von SchachtZeilenMenue nicht gefunden");
        return xaml[start..end];
    }

    [Fact]
    public void Spaltenaufbau_pro_Projekt_loest_die_Ansicht_nach_ohne_weitere_Zeile_in_der_Codebehind()
    {
        var code = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechtePage.ColumnViews.cs"));
        Assert.Contains("Grid.Columns.CollectionChanged", code);
        Assert.Contains("_reapplyGeplant", code);
    }

    [Fact]
    public void Schaechte_haben_Schachtansicht_rechts_und_Eingabefelder_unten_mit_gespeicherten_Trennlinien()
    {
        var xaml = Xaml();
        Assert.Contains("schachtansicht:SchachtUebersichtPanel", xaml);
        Assert.Contains("haltung:HaltungFelderDrawer", xaml);
        Assert.Contains("SplitterKey=\"SchaechteSchachtansicht\"", xaml);
        Assert.Contains("SplitterKey=\"SchaechteEingabefelder\"", xaml);
        Assert.Contains("ViewPersonalization.ViewKey=\"SchaechtePage\"", xaml);
        var settings = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "AppSettings.cs"));
        Assert.Contains("public bool ShowSchaechteNovaLayout { get; set; } = true;", settings);
    }

    [Fact]
    public void Schachtansicht_erklaert_die_Handbewertung_und_zeigt_die_Schachtgrafik()
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "Schachtansicht", "SchachtUebersichtPanel.xaml"));
        Assert.Contains("Am Schacht wird die Zustandsklasse nie berechnet", xaml);
        Assert.Contains("<local:SchachtgrafikControl", xaml);
        Assert.Contains("ZustandsklasseInkConverter", xaml);
    }

    /// <summary>
    /// Nova, Aufklapp-Liste (Task 5): Die Schachtgrafik ersetzt den frueheren Grundriss-Kreis
    /// (senkrechter Schnitt statt Draufsicht) und bekommt Haltungen und Katalog von der Seite
    /// gereicht — kein Service-Locator im Panel oder im Control.
    /// </summary>
    [Fact]
    public void Schachtgrafik_ersetzt_den_Grundriss_und_bekommt_Haltungen_und_Katalog_von_der_Seite()
    {
        var panelXaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "Schachtansicht", "SchachtUebersichtPanel.xaml"));
        Assert.DoesNotContain("AutomationProperties.Name=\"Schachtgrundriss\"", panelXaml);
        Assert.DoesNotContain("x:Name=\"Kreis\"", panelXaml);
        Assert.DoesNotContain("x:Name=\"Oval\"", panelXaml);
        Assert.DoesNotContain("x:Name=\"Quadrat\"", panelXaml);
        Assert.Contains("Haltungen=\"{Binding Haltungen, ElementName=Root}\"", panelXaml);
        Assert.Contains("Catalog=\"{Binding Catalog, ElementName=Root}\"", panelXaml);

        var controlCode = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "Schachtansicht", "SchachtgrafikControl.xaml.cs"));
        Assert.DoesNotContain("App.Services", controlCode);
        Assert.DoesNotContain("ServiceProvider.Current", controlCode);
    }

    /// <summary>
    /// Nova-Etappe 2b, Task 4: Suche als Pille rechts, gleiches Muster wie die globale Suche
    /// in MainWindow.xaml (RadiusPill, InputBorderBrush, Lupe). Ohne F3-Marke - die bleibt den
    /// Haltungen vorbehalten.
    /// </summary>
    [Fact]
    public void Werkzeugleiste_zeigt_die_Suche_als_Pille_ohne_F3_Marke()
    {
        var xaml = Xaml();
        Assert.Contains("{DynamicResource RadiusPill}", xaml);
        Assert.Contains("{DynamicResource InputBorderBrush}", xaml);
        Assert.Contains("Suche Schacht", xaml);
        Assert.DoesNotContain("Text=\"F3\"", xaml);
        Assert.DoesNotContain("PreviewKeyDown=\"", xaml);
    }

    /// <summary>
    /// Nova-Etappe 2b, Task 6: Ohne gewaehlte Zeile zeigt die Schachtansicht NUR den
    /// Leerzustand — keine Schachtgrafik, keine leeren Beschriftungen, keine Knoepfe.
    /// Task 5: <c>Inhalt</c> ist seit der Schachtgrafik ein <c>ScrollViewer</c> (Muster
    /// <c>HaltungUebersichtPanel</c>), kein <c>DockPanel</c> mehr — ohne Bildlauf wuerde die
    /// mindestens 320 px hohe Grafik die Eckdaten und die Schadenliste abschneiden.
    /// </summary>
    [Fact]
    public void Schachtansicht_zeigt_ohne_Auswahl_nur_den_Leerzustand()
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "Schachtansicht", "SchachtUebersichtPanel.xaml"));
        Assert.Contains("x:Name=\"Leerzustand\"", xaml);
        Assert.Contains("Kein Schacht gewählt. Links eine Zeile wählen.", xaml);

        var inhalt = Regex.Match(xaml, @"<ScrollViewer x:Name=""Inhalt""[\s\S]*?</ScrollViewer.Style>");
        Assert.True(inhalt.Success, "Inhalt der Schachtansicht braucht einen eigenen Sichtbarkeitsschalter");
        Assert.Contains("<DataTrigger Binding=\"{Binding Record, ElementName=Root}\" Value=\"{x:Null}\">", inhalt.Value);
        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Collapsed\"/>", inhalt.Value);
    }

    /// <summary>
    /// Task 6: Die Zustandsklasse der Schachtliste verwendet die gemeinsame Marke, das
    /// Protokoll den gemeinsamen Knopf. Keine zweite Schachtfabrik daneben.
    /// </summary>
    [Fact]
    public void Zustandsklasse_und_Protokoll_kommen_aus_den_gemeinsamen_Fabriken()
    {
        var code = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechtePage.xaml.cs"));
        var protokoll = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechtePage.Protokollspalte.cs"));

        Assert.Contains("ZustandsklasseChipColumnFactory.Create(", code);
        Assert.DoesNotContain("SchaechteZustandsklasseColumnFactory", code);
        Assert.Contains("SchaechteProtokollColumnFactory.Create(", protokoll);
        Assert.False(
            File.Exists(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechteZustandsklasseColumnFactory.cs")),
            "Die eigene Schachtfabrik ist durch die gemeinsame Marke ersetzt.");
    }

    /// <summary>
    /// Task 6 (Review-Minor aus Task 1): Der Anzeigename einer Spalte wird genau EINMAL geholt
    /// und danach gross geschrieben. Vorher setzte der Textspalten-Zweig den Kopf selbst und
    /// direkt darunter wurde er nochmals gelesen und ueberschrieben.
    /// </summary>
    [Fact]
    public void Tabellenkopf_wird_einmal_geholt_und_gross_geschrieben()
    {
        var code = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechtePage.xaml.cs"));
        var aufbau = Regex.Match(code, @"private void RebuildColumns\(\)[\s\S]*?\n    \}");
        Assert.True(aufbau.Success, "RebuildColumns nicht gefunden");

        Assert.Single(Regex.Matches(aufbau.Value, @"GetDisplayHeader\("));
        Assert.Contains("GrossbuchstabenConverter.Anwenden(kopf)", aufbau.Value);
    }

    /// <summary>
    /// Task 6, Fix-Runde 1: Eine echte Auswahl in der Zustandsklassen-Marke muss als
    /// Handeingabe gestempelt werden — nur handgesetzte Felder gehen in die XTF. Die Seite merkt
    /// sich dafuer den Wert beim Oeffnen der Zelle und schreibt beim Schliessen nur bei echter
    /// Aenderung; das Projekt gilt danach als geaendert.
    /// </summary>
    [Fact]
    public void Eine_Auswahl_der_Zustandsklasse_wird_gestempelt_und_meldet_die_Aenderung()
    {
        var code = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechtePage.xaml.cs"));

        Assert.Contains("_zustandsklasseBeimOeffnen", code);
        var commit = Regex.Match(code, @"private void Grid_CellEditEnding[\s\S]*?\n    \}");
        Assert.True(commit.Success, "Grid_CellEditEnding nicht gefunden");
        Assert.Contains("SchaechteFieldEditController.ApplyZustandsklasse(", commit.Value);
        Assert.Contains("MarkProjectDirty();", commit.Value);
    }

    /// <summary>
    /// Task 6, Fix-Runde 1: Knopf und Gedankenstrich der Statusspalten kommen in beiden Listen
    /// aus demselben Baustein — sonst driften Stil, Hinweis und vorlesbarer Name auseinander.
    /// </summary>
    [Fact]
    public void Knopfzellen_beider_Listen_kommen_aus_einem_Baustein()
    {
        foreach (var datei in new[] { "HaltungStatusColumnFactory.cs", "SchaechteProtokollColumnFactory.cs" })
        {
            var code = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", datei));
            Assert.Contains("StatusZellenBausteine.Aktionsknopf(", code);
            Assert.Contains("StatusZellenBausteine.Fehlt(", code);
        }
    }

    /// <summary>
    /// Task 6, Fix-Runde 1: Auch die Schachtansicht zeigt fuer ein leeres Eckdatenfeld den
    /// Gedankenstrich statt einer leeren Zeile unter der Beschriftung.
    /// </summary>
    [Fact]
    public void Schachtansicht_zeigt_leere_Eckdaten_als_Gedankenstrich()
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "Schachtansicht", "SchachtUebersichtPanel.xaml"));
        Assert.Contains("haltung:FaktWertConverter", xaml);
        foreach (var feld in new[] { "Funktion", "Material", "Schachttiefe", "Baujahr", "Belastungsklasse", "Inspektionsdatum" })
            Assert.Contains($"Fields[{feld}], Converter={{StaticResource FaktWertConv}}", xaml);
    }

    /// <summary>
    /// Nova, Aufklapp-Liste (2026-09-08, Task 6 des Plans "Haltungen als Aufklapp-Liste"):
    /// Aufklapp-Liste und Tabelle sind eine Gruppe im Menue "Weitere Aktionen", genau wie bei den
    /// Haltungen. Optikanalyse 28.09.2026, Aufgabe 8: Die frühere dritte Gruppengenossin "Alte
    /// Schachtansicht" ist aus diesem Menü entfernt (Einstellungen -> Allgemein -> Frühere
    /// Ansichten); das MenuItem SchachtansichtToggle bleibt nur unsichtbar ausserhalb dieses
    /// Menüs als Zustandshalter fuer SchaechteAnsichtUmschalter stehen.
    /// </summary>
    [Fact]
    public void Ansicht_Liste_und_Tabelle_stehen_als_Gruppe()
    {
        var xaml = Xaml();
        Assert.Contains("x:Name=\"AnsichtListeMenu\" Header=\"Aufklapp-Liste\" IsCheckable=\"True\" Tag=\"liste\"", xaml);
        Assert.Contains("x:Name=\"AnsichtTabelleMenu\" Header=\"Tabelle\" IsCheckable=\"True\" Tag=\"tabelle\"", xaml);
        Assert.Contains("Click=\"AnsichtMenu_Click\"", xaml);
        Assert.DoesNotContain("Header=\"Alte Schachtansicht\"", xaml);

        // SchachtansichtToggle steht direkt hinter dem geschlossenen WeitereAktionenDropdown-
        // Button, ausserhalb jedes Button.ContextMenu.
        Assert.Matches(new Regex(
            @"</Button\.ContextMenu>\s*</Button>\s*(?:<!--[\s\S]*?-->\s*)*<MenuItem x:Name=""SchachtansichtToggle"""),
            xaml);
        var toggle = Regex.Match(xaml, "<MenuItem x:Name=\"SchachtansichtToggle\"[\\s\\S]*?/>");
        Assert.True(toggle.Success, "SchachtansichtToggle muss ein MenuItem sein");
        Assert.Contains("Visibility=\"Collapsed\"", toggle.Value);
        Assert.DoesNotContain("Header=", toggle.Value);

        var settings = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "AppSettings.cs"));
        Assert.Contains("public string SchaechteAnsicht { get; set; } = \"liste\";", settings);
    }

    /// <summary>
    /// Task 6: Die Schaechte-Liste bindet dieselbe Sammlung und dasselbe Zeilen-Kontextmenue
    /// wie die Tabelle — kein zweiter Weg auf Auswahl, Suche oder Protokoll-Oeffner.
    /// </summary>
    [Fact]
    public void Aufklapp_Liste_bindet_dieselbe_Sammlung_und_dasselbe_Kontextmenue_wie_die_Tabelle()
    {
        var xaml = Xaml();
        Assert.Contains("<schachtansicht:SchachtAufklappListe x:Name=\"AufklappListe\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Records}\"", xaml);
        Assert.Contains("SelectedItem=\"{Binding Selected, Mode=TwoWay}\"", xaml);
        Assert.Contains("ContextMenu=\"{StaticResource SchachtZeilenMenue}\"", xaml);
        Assert.Contains("ZeilenMenue=\"{StaticResource SchachtZeilenMenue}\"", xaml);
        Assert.Contains(
            "ProtokollCommand=\"{Binding ProtokollOeffnenCommand, RelativeSource={RelativeSource AncestorType={x:Type local:SchaechtePage}}}\"",
            xaml);
    }

    /// <summary>
    /// Task 6: Formularaufbau, Live-Abgleich, Tastenregel und Themenbildung der Schacht-Liste
    /// sind geteilte Bausteine mit den Haltungen — keine zweite Fassung derselben Logik.
    /// </summary>
    [Fact]
    public void Schacht_Aufklapp_Liste_verwendet_die_geteilten_Bausteine_der_Haltungen()
    {
        var controllerCode = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "DataPage", "SchaechteAufklappListeController.cs"));
        Assert.Contains("AufklappDetailLayout.Themen(", controllerCode);
        var haltungController = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "DataPage", "DataPageAufklappListeController.cs"));
        Assert.Contains("AufklappDetailLayout.Themen(", haltungController);
        var layoutCode = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "DataPage", "AufklappDetailLayout.cs"));
        Assert.Contains("HaltungThemenGruppierung.Bilde(", layoutCode);
        Assert.Contains("new DataPageDetailLiveSync(", controllerCode);

        var controlCode = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "Schachtansicht", "SchachtAufklappListe.xaml.cs"));
        Assert.Contains("HaltungAufklappTastenregel.Bestimme(", controlCode);

        var umschalterCode = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "DataPage", "SchaechteAnsichtUmschalter.cs"));
        Assert.Contains("HaltungenAnsichtRegel.Bestimme(", umschalterCode);
        Assert.Contains("HaltungenAnsichtRegel.Normalisiere(", umschalterCode);
    }
}
