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
        foreach (var header in new[] { "PDF-Daten", "Aktualisieren", "Leere Felder aus QGIS", "Katasterkennungen", "Feldnamen aufräumen", "Strassen", "Hoch", "Runter", "Ansicht anpassen", "Alte Schachtansicht" })
            Assert.Contains($"Header=\"{header}\"", xaml);
        Assert.DoesNotContain("<ToggleButton x:Name=\"SchachtansichtToggle\"", xaml);
        Assert.Contains("x:Name=\"ColumnViewChips\"", xaml);
        Assert.Contains("SchaechteColumnViewCatalog.Views", xaml);
    }

    [Fact]
    public void Gruppen_im_Menue_trennen_nur_mit_Separator_ohne_deaktivierte_Kopfzeilen()
    {
        var xaml = Xaml();
        Assert.DoesNotContain("IsEnabled=\"False\" Focusable=\"False\"", xaml);
        Assert.Matches(new Regex("<Separator/>\\s*<MenuItem Header=\"Hoch\""), xaml);
        Assert.Matches(new Regex("<Separator/>\\s*<MenuItem Header=\"Sanierungsmassnahmen\\.\\.\\.\""), xaml);
        Assert.Matches(new Regex("<Separator/>\\s*<MenuItem Header=\"Ansicht anpassen\""), xaml);
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
    public void Schachtansicht_erklaert_die_Handbewertung_und_zeigt_den_Grundriss()
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "Schachtansicht", "SchachtUebersichtPanel.xaml"));
        Assert.Contains("Am Schacht wird die Zustandsklasse nie berechnet", xaml);
        Assert.Contains("AutomationProperties.Name=\"Schachtgrundriss\"", xaml);
        Assert.Contains("ZustandsklasseInkConverter", xaml);
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
    /// Leerzustand — kein Grundriss, keine leeren Beschriftungen, keine Knoepfe.
    /// </summary>
    [Fact]
    public void Schachtansicht_zeigt_ohne_Auswahl_nur_den_Leerzustand()
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "Schachtansicht", "SchachtUebersichtPanel.xaml"));
        Assert.Contains("x:Name=\"Leerzustand\"", xaml);
        Assert.Contains("Kein Schacht gewählt. Links eine Zeile wählen.", xaml);

        var inhalt = Regex.Match(xaml, @"<DockPanel x:Name=""Inhalt""[\s\S]*?</DockPanel.Style>");
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

        Assert.Equal(1, Regex.Matches(aufbau.Value, @"GetDisplayHeader\(").Count);
        Assert.Contains("GrossbuchstabenConverter.Anwenden(kopf)", aufbau.Value);
    }
}
