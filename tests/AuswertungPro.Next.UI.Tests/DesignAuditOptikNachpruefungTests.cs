using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter der optischen Nachpruefung vom 08.09.2026
/// (docs/reviews/2026-09-08-redesign-gesamtaudit/OPTIK-NACHPRUEFUNG.md). Jeder Test haelt
/// einen dort belegten Befund fest, damit er nicht zurueckfaellt. Reine Quelltext-Pruefungen
/// nach dem Muster der uebrigen DesignAudit*-Klassen.
/// </summary>
public sealed class DesignAuditOptikNachpruefungTests
{
    private static string Ui(params string[] teile)
        => File.ReadAllText(RepoFile(new[] { "src", "AuswertungPro.Next.UI" }.Concat(teile).ToArray()));

    /// <summary>O16: Die Zustandsklasse ist eine Marke mit Tintenregel, kein gelber Text auf Weiss.</summary>
    [Fact]
    public void Schattenauswertung_zeigt_die_Zustandsklasse_als_Marke_mit_Tintenregel()
    {
        var xaml = Ui("Views", "Pages", "SchattenauswertungPage.xaml");
        Assert.Contains("ZustandsklasseInkConverter", xaml);
        Assert.DoesNotContain("Foreground=\"{Binding MenschKlasse, Converter={StaticResource ZustandsklasseBrush}}\"", xaml);
        Assert.DoesNotContain("Foreground=\"{Binding SchattenKlasse, Converter={StaticResource ZustandsklasseBrush}}\"", xaml);
        Assert.Equal(2, Regex.Matches(xaml, "CornerRadius=\"\\{DynamicResource RadiusChip\\}\"").Count);
    }

    /// <summary>O17: Das Erfolgs-Abzeichen nimmt die Text-Tinte, nicht die Flaechenfarbe.</summary>
    [Fact]
    public void Pruefung_Abzeichen_verwenden_die_Text_Tinte_auf_der_Erfolgsflaeche()
    {
        var code = Ui("Views", "Pages", "HaltungStatusColumnFactory.cs");
        Assert.Contains("Kapsel(HaltungPruefstand.Abgeschlossen, \"SuccessSubtleBrush\", \"SuccessTextBrush\")", code);
        Assert.DoesNotContain("\"SuccessSubtleBrush\", \"SuccessBrush\"", code);
    }

    /// <summary>
    /// O18: Die ZK-Filterchips nehmen ihre Tinte aus der Ink-Regel und reichen sie an der
    /// B7-Falle vorbei (impliziter TextBlock-Stil) bis zur Ziffer durch.
    /// </summary>
    [Fact]
    public void Zustandsklassen_Filterchips_nehmen_die_Tinte_aus_der_Ink_Regel()
    {
        var code = Ui("Controls", "FilterChipBar.xaml.cs");
        Assert.DoesNotContain("Brushes.Black", code);
        Assert.Contains("ZustandsklasseInkPolicy.InkFor", code);

        var xaml = Ui("Controls", "FilterChipBar.xaml");
        Assert.Contains("RelativeSource={RelativeSource AncestorType=ToggleButton}", xaml);
    }

    /// <summary>O7: Leere Suchfelder tragen einen Platzhalter; leere Statuszeilen verschwinden.</summary>
    [Fact]
    public void Suchfelder_tragen_Platzhalter_und_leere_Statuszeilen_verschwinden()
    {
        var drawer = Ui("Views", "Pages", "Haltungsansicht", "HaltungFelderDrawer.xaml");
        Assert.Contains("Text=\"Feld suchen\"", drawer);
        Assert.Contains("Binding=\"{Binding Text, ElementName=FeldSuche}\" Value=\"\"", drawer);

        var settings = Ui("Views", "Pages", "SettingsPage.xaml");
        Assert.Contains("Text=\"Einstellung suchen\"", settings);
        Assert.Contains("Binding=\"{Binding Text, ElementName=SucheBox}\" Value=\"\"", settings);

        var export = Ui("Views", "Pages", "ExportPage.xaml");
        Assert.Contains("<DataTrigger Binding=\"{Binding LastResult}\" Value=\"\">", export);

        var dossiers = Ui("Views", "Pages", "DossiersPage.xaml");
        Assert.Contains("<Condition Binding=\"{Binding StatusMessage}\" Value=\"\"/>", dossiers);
        Assert.Contains("<Condition Binding=\"{Binding IsBusy}\" Value=\"False\"/>", dossiers);
    }

    /// <summary>O10: Kein gruener Haken vor „Noch keine Berechnung" — das Symbol folgt HatErgebnis.</summary>
    [Fact]
    public void VSA_Ergebnis_Symbol_folgt_dem_Zustand()
    {
        var xaml = Ui("Views", "Pages", "VsaPage.xaml");
        Assert.Contains("<DataTrigger Binding=\"{Binding HatErgebnis}\" Value=\"True\">", xaml);
        Assert.DoesNotContain("Text=\"&#xE73E;\" FontFamily=\"{DynamicResource FontIcon}\"", xaml);

        var vm = Ui("ViewModels", "Pages", "VsaPageViewModel.cs");
        Assert.Contains("private bool _hatErgebnis;", vm);
        Assert.Contains("HatErgebnis = true;", vm);
        Assert.Contains("HatErgebnis = false;", vm);
    }
}
