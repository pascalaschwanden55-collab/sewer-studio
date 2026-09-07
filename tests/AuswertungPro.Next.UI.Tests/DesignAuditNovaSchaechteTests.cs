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
}
