using System.Text.RegularExpressions;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditCommandReachabilityTests
{
    [Fact]
    public void Main_menu_exposes_both_cost_catalog_editors()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml"));

        Assert.Contains("Command=\"{Binding OpenPriceCatalogCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenTemplateEditorCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Holding_context_menu_exposes_video_ai_pipeline_for_selected_record()
    {
        var xaml = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml"));

        Assert.Contains("PlacementTarget.DataContext.OpenVideoAiPipelineCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("CommandParameter=\"{Binding PlacementTarget.SelectedItem", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"KI-Videoanalyse", xaml, StringComparison.Ordinal);
    }

    // Nova-Etappe 1: Die Werkzeugleiste zeigt eine Hauptaktion; alles andere liegt unter
    // "Weitere Aktionen". Kein sichtbarer Aktionstext darf dabei verschwinden.
    // Optikanalyse 28.09.2026, Aufgabe 7: fuenf Untermenues (Daten abgleichen, Bearbeiten,
    // Reihenfolge, Ansicht, Ausgabe) ersetzen die flache Liste. "Sanierungsmaßnahme bearbeiten"
    // ist als Doppelung zum Zeilenmenue entfernt (dort "Sanierungsmassnahmen..."),
    // "Fokusmodus (F11)" als Doppelung zum Menue Ansicht.
    [Theory]
    [InlineData("Speichern")]
    [InlineData("Neu")]
    [InlineData("Löschen")]
    [InlineData("Video prüfen")]
    [InlineData("Leere Felder aus QGIS")]
    [InlineData("GeoShop-Abgleich (XTF)")]
    [InlineData("Direkt zur KI-Optimierung")]
    [InlineData("Vorschlag für diese Haltung erstellen")]
    [InlineData("Medien suchen")]
    [InlineData("Strassennamen ergänzen")]
    [InlineData("Hydraulik berechnen")]
    [InlineData("Hydraulik PDF")]
    [InlineData("Dossier")]
    [InlineData("Abdocken")]
    [InlineData("Spalten anordnen")]
    [InlineData("Spalte leeren")]
    [InlineData("Zeilenhöhe:")]
    [InlineData("Zoom:")]
    [InlineData("Ausrichtung:")]
    [InlineData("Haltungsansicht")]
    [InlineData("Weitere Aktionen")]
    [InlineData("Daten abgleichen")]
    [InlineData("Bearbeiten")]
    [InlineData("Reihenfolge")]
    [InlineData("Ansicht")]
    [InlineData("Ausgabe")]
    [InlineData("Nach oben")]
    [InlineData("Nach unten")]
    [InlineData("Auf Position…")]
    [InlineData("Gehe zu Zeile…")]
    public void Haltungen_toolbar_keeps_every_action_reachable(string sichtbarerText)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml"));
        Assert.Contains(sichtbarerText, xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Haltungen_toolbar_has_exactly_one_primary_button()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml"));
        var toolbar = xaml[..xaml.IndexOf("x:Name=\"GridHost\"", StringComparison.Ordinal)];
        Assert.Single(Regex.Matches(toolbar, "Style=\"\\{StaticResource ToolbarButtonAccent\\}\""));
    }
}
