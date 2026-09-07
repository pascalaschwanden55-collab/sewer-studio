using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditNovaTrainingStudioTests
{
    private static string Xaml() => File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingStudioWindow.xaml"));

    [Fact]
    public void Drei_Spalten_mit_Prototyp_Breiten_und_Titelchip()
    {
        var xaml = Xaml();
        // B4: Das Prototypmass 210 bleibt die MINDESTbreite der Werkzeugspalte; sie darf auf
        // hoechstens 240 wachsen, damit nichts abgeschnitten wird.
        var werkzeugspalte = Regex.Match(xaml, "<ColumnDefinition Width=\"(?<w>[0-9]+)\" MinWidth=\"210\" MaxWidth=\"240\"/>");
        Assert.True(werkzeugspalte.Success, "Werkzeugspalte ohne MinWidth 210 / MaxWidth 240");
        var breite = int.Parse(werkzeugspalte.Groups["w"].Value);
        Assert.InRange(breite, 210, 240);
        Assert.Contains("<ColumnDefinition Width=\"330\"/>", xaml);
        // Ohne diese Zeile misst die Spalte ihre Kinder mit unendlicher Breite: Texte brechen
        // dann nicht um und der Rand schneidet sie ab.
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\" Margin=\"0,0,12,0\"", xaml);
        Assert.Contains("Text=\"{Binding KiBereitschaftText}\"", xaml);
        Assert.Contains("Training Studio (Prüfplatz)", xaml);
    }

    [Fact]
    public void Rechte_Spalte_hat_drei_nummerierte_Schritte_und_keinen_Schein_Freigabeknopf()
    {
        var xaml = Xaml();
        Assert.Contains("1 · KI-Vorschlag", xaml);
        Assert.Contains("2 · Fachliche Codierung", xaml);
        Assert.Contains("3 · Freigabe für Training", xaml);
        Assert.Contains("Training Center öffnen", xaml);
        Assert.DoesNotContain("Für Training freigeben", xaml);
    }

    [Fact]
    public void Alle_bisherigen_Aktionen_bleiben_erreichbar()
    {
        var xaml = Xaml();
        foreach (var t in new[] { "Fotos laden…", "PDF laden…", "PDF-Ordner laden…", "Gold-Eingang öffnen", "Eingang laden", "Warteschlange laden", "Segmentierung abarbeiten", "Goldprüfung (90)", "Alle Gold-Reparaturfälle", "Goldalbum", "KI starten", "Akzeptieren (A)", "Korrektur speichern (K)", "Verwerfen (V)", "Nächstes (→)", "Codieren… (Katalog)", "Foto mit gewähltem Modell prüfen", "Foto allgemein mit KI prüfen" })
            Assert.Contains($"Content=\"{t}\"", xaml);
    }

    [Fact]
    public void Overlay_beschriftet_Hand_Box_und_Maske()
    {
        var cs = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingStudioWindow.xaml.cs"));
        Assert.Contains("\"Hand-Box\"", cs);
        Assert.Contains("StatusBadgeTextBrush", cs);
    }
}
