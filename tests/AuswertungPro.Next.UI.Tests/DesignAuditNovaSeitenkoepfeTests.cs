using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Nova-Etappe 2, Teil J: Einheitlicher Seitenkopf mit Untertitel auf allen Seiten.</summary>
public sealed class DesignAuditNovaSeitenkoepfeTests
{
    [Theory]
    [InlineData("ProjectPage.xaml", "Stammdaten des offenen Projekts")]
    [InlineData("ImportPage.xaml", "Kanalfernseh-Projekte, Protokolle, Medien")]
    [InlineData("ExportPage.xaml", "Excel, Verteilung, Kataster")]
    [InlineData("MediaConflictsPage.xaml", "Videos, die keiner Haltung sicher zugeordnet sind")]
    [InlineData("BuilderPage.xaml", "Listen, Statistik und NPK-Leistungsverzeichnis")]
    [InlineData("DossiersPage.xaml", "eine Liegenschaft, ihre Leitungen und Schächte")]
    [InlineData("SchattenauswertungPage.xaml", "dein Urteil neben dem der KI, ändert keine Projektdaten")]
    [InlineData("VsaPage.xaml", "Zustandsklasse und Noten nach VSA-KEK 2020")]
    [InlineData("DiagnosticsPage.xaml", "Protokoll des laufenden Programms")]
    public void Seite_traegt_den_Nova_Seitenkopf_mit_Prototyp_Untertitel(string datei, string untertitel)
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", datei));
        // Fix-Runde 1: Praefix-unabhaengig pruefen (MediaConflictsPage verwendet den vorhandenen
        // Alias "controls:", nicht "ctrl:" - ein zweiter Alias auf denselben Namespace ist unnoetig).
        Assert.Contains(":NovaPageHeader", xaml);
        Assert.Contains($"Subtitle=\"{untertitel}\"", xaml);
    }

    [Fact]
    public void Seitenkopf_verwendet_den_PageTitle_Stil()
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Controls", "NovaPageHeader.xaml"));
        // Ruling (Fix-Runde 1): Nur DynamicResource ist fuer das Control zulaessig - der Style
        // liegt in App.Resources; StaticResource wuerde die Theme-Farbe der Titel-Unterstreichung
        // beim Hell/Dunkel-Wechsel einfrieren.
        Assert.Contains("Style=\"{DynamicResource PageTitle}\"", xaml);
        Assert.DoesNotContain("Style=\"{StaticResource PageTitle}\"", xaml);
    }

    // SanierungsMatrixPage und SchachtSanierungsMatrixPage binden Titel UND Untertitel dynamisch
    // an das ViewModel (modusabhaengige Texte, siehe SanierungsMatrixPageViewModel). Der bestehende
    // Waechter SanierungsMatrixPage_zeigt_massnahmen_spalte_und_lesedetail verlangt dort woertlich
    // Text="{Binding PageTitle}" / Text="{Binding PageSubtitle}" - der NovaPageHeader wird deshalb
    // bewusst NICHT eingesetzt. Hier nur pruefen, dass beide Seiten die Untertitel-Bindung tragen.
    [Theory]
    [InlineData("SanierungsMatrixPage.xaml")]
    [InlineData("SchachtSanierungsMatrixPage.xaml")]
    public void Sanierungs_Matrix_Seiten_binden_den_Untertitel_statt_NovaPageHeader(string datei)
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", datei));
        Assert.Contains("Text=\"{Binding PageTitle}\"", xaml);
        Assert.Contains("Text=\"{Binding PageSubtitle}\"", xaml);
        Assert.DoesNotContain(":NovaPageHeader", xaml);
    }

    [Fact]
    public void Einstellungen_traegt_den_Nova_Seitenkopf_ohne_Untertitel()
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SettingsPage.xaml"));
        Assert.Contains(":NovaPageHeader", xaml);
        // Fix-Runde 1: nur der Start-Tag des Controls darf kein Subtitle= tragen - ein "Subtitle="
        // irgendwo sonst in der Datei (z. B. in einem Kommentar oder einer anderen Seite) soll den
        // Test nicht faelschlich rot machen.
        Assert.DoesNotContain("Subtitle=", NovaPageHeaderStartTag(xaml));
    }

    private static string NovaPageHeaderStartTag(string xaml)
    {
        var markerIndex = xaml.IndexOf(":NovaPageHeader", System.StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "NovaPageHeader-Start-Tag wurde nicht gefunden.");

        var tagStart = xaml.LastIndexOf('<', markerIndex);
        Assert.True(tagStart >= 0, "Öffnendes '<' des NovaPageHeader-Start-Tags wurde nicht gefunden.");

        var tagEnd = xaml.IndexOf('>', markerIndex);
        Assert.True(tagEnd >= 0, "Schliessendes '>' des NovaPageHeader-Start-Tags wurde nicht gefunden.");

        return xaml[tagStart..(tagEnd + 1)];
    }
}
