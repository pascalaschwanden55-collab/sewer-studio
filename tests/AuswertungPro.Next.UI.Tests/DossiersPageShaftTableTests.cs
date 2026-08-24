using System;
using System.IO;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.UI.ViewModels.Pages;

using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Prueft die Schacht-Tabelle des Dossier-Cockpits — das Gegenstueck zur
/// Leitungstabelle.
/// </summary>
public sealed class DossiersPageShaftTableTests
{
    private static string Seite() => File.ReadAllText(RepoFile(
        "src", "AuswertungPro.Next.UI", "Views", "Pages", "DossiersPage.xaml"));

    [Fact]
    public void Der_Knopf_Schaechte_waehlen_steht_neben_dem_Leitungsknopf()
    {
        var xaml = Seite();

        var knopf = xaml.IndexOf("Content=\"Schächte wählen…\"", StringComparison.Ordinal);
        Assert.True(knopf >= 0, "Es gibt keinen Knopf „Schächte wählen…\".");

        var abschnitt = xaml[knopf..Math.Min(xaml.Length, knopf + 400)];
        Assert.Contains(
            "Command=\"{Binding EditShaftsCommand}\"", abschnitt, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Seite_zeigt_eine_eigene_Schacht_Tabelle()
    {
        var xaml = Seite();

        Assert.Contains("ItemsSource=\"{Binding ShaftRows}\"", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Schacht")]
    [InlineData("Funktion")]
    [InlineData("Empfohlene Massnahme")]
    [InlineData("Kosten CHF")]
    public void Die_Schacht_Tabelle_fuehrt_die_vereinbarten_Spalten(string ueberschrift)
    {
        var xaml = Seite();

        var tabelle = xaml.IndexOf(
            "ItemsSource=\"{Binding ShaftRows}\"", StringComparison.Ordinal);
        Assert.True(tabelle >= 0, "Die Schacht-Tabelle fehlt.");

        var abschnitt = xaml[tabelle..];
        Assert.Contains($"Header=\"{ueberschrift}\"", abschnitt, StringComparison.Ordinal);
    }

    [Fact]
    public void Zwischen_beiden_Tabellen_liegt_ein_verschiebbarer_Trenner()
    {
        // Ohne Trenner haetten Leitungen und Schaechte eine feste Aufteilung —
        // bei fuenf Leitungen und einem Schacht waere die halbe Seite leer.
        Assert.Contains("<GridSplitter", Seite(), StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_Schachtzeile_zeigt_Nummer_Funktion_Massnahme_und_Kosten()
    {
        var zeile = DossiersPageViewModel.BuildShaftRow(new DossierShaftLine(
            Guid.NewGuid(),
            "80551",
            "Jagdmattweg",
            "ohne",
            1100m,
            "Kontrollschacht",
            "Schachthals sanieren; Fugen sanieren"));

        Assert.Equal("80551", zeile.Shaft);
        Assert.Equal("Kontrollschacht", zeile.Funktion);
        Assert.Equal("Schachthals sanieren; Fugen sanieren", zeile.Measures);
        // de-CH trennt Tausender mit dem typografischen Apostroph U+2019 —
        // dieselbe Kultur und dasselbe Format wie die Kostenspalte der Leitungen.
        Assert.Equal("1’100.00", zeile.Cost);
    }

    [Fact]
    public void Fehlende_Angaben_erscheinen_als_Strich_und_nicht_als_Null()
    {
        // Eine leere Zelle liesse offen, ob nichts erfasst oder nichts noetig
        // ist; "0.00" waere eine Zahl, die niemand geprueft hat.
        var zeile = DossiersPageViewModel.BuildShaftRow(new DossierShaftLine(
            Guid.NewGuid(), "36051", "", "ohne", 0m));

        Assert.Equal("36051", zeile.Shaft);
        Assert.Equal("—", zeile.Funktion);
        Assert.Equal("—", zeile.Measures);
        Assert.Equal("—", zeile.Cost);
    }

    [Theory]
    [InlineData(0, "Kein Schacht zugeordnet.")]
    [InlineData(1, "1 Schacht zugeordnet.")]
    [InlineData(4, "4 Schächte zugeordnet.")]
    public void Die_Rueckmeldung_der_Auswahl_nennt_die_Anzahl(int anzahl, string erwartet)
    {
        Assert.Equal(erwartet, DossiersPageViewModel.SchaechteZugeordnet(anzahl));
    }

    [Fact]
    public void Ein_misslungenes_Speichern_laesst_die_alte_Schachtauswahl_stehen()
    {
        var quelle = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages",
            "DossiersPageViewModel.Actions.cs"));

        var start = quelle.IndexOf(
            "private async Task EditShaftsAsync()", StringComparison.Ordinal);
        Assert.True(start >= 0, "EditShaftsAsync fehlt.");

        var ende = quelle.IndexOf("\n    private", start + 10, StringComparison.Ordinal);
        Assert.True(ende > start, "Das Ende von EditShaftsAsync wurde nicht gefunden.");

        var rumpf = quelle[start..ende];
        Assert.Contains("EnsureProject(out var root)", rumpf, StringComparison.Ordinal);
        Assert.Contains("ShaftNumbers = vorher", rumpf, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Schachtkosten_kommen_aus_beiden_gepflegten_Dateien()
    {
        // Matrix vor Empfehlung, niemals addiert — dieselbe Regel wie im
        // Druckcenter. Eine eigene zweite Regel stuende sonst dem Kunden
        // gegenueber mit einem anderen Betrag da.
        var quelle = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages",
            "DossiersPageViewModel.cs"));

        Assert.Contains("SchachtCostStoreMerger.Merge", quelle, StringComparison.Ordinal);
        Assert.Contains("schacht_costs.json", quelle, StringComparison.Ordinal);
        Assert.Contains("schacht_empfehlungen.json", quelle, StringComparison.Ordinal);
    }
}
