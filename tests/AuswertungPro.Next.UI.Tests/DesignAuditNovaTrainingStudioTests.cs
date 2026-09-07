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
        Assert.Contains("<ColumnDefinition Width=\"210\"/>", xaml);
        Assert.Contains("<ColumnDefinition Width=\"330\"/>", xaml);
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
