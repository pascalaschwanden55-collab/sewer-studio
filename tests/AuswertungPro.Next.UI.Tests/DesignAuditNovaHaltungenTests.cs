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
}
