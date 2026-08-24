using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Platz im Dossier-Cockpit.
///
/// Kennzahlen und Zustandsblock brauchen rund ein Drittel der Höhe, obwohl man
/// sie beim Arbeiten selten liest — von fünf Leitungen blieben drei sichtbar.
/// Zugeklappt bleibt eine Zeile stehen: die Zahlen sollen nicht verschwinden,
/// nur der Platz.
/// </summary>
public sealed class DossiersPageSummaryLineTests
{
    private static DossierSnapshot Stand(
        int leitungen = 0,
        int schaechte = 0,
        double laenge = 0,
        decimal kosten = 0,
        int dringend = 0)
    {
        var holdings = Enumerable.Range(0, leitungen)
            .Select(i => new DossierHoldingLine(
                Guid.NewGuid(), $"H-{i}", "", laenge / Math.Max(leitungen, 1),
                dringend > i ? "1" : "4", kosten / Math.Max(leitungen, 1), ""))
            .ToList();

        var shafts = Enumerable.Range(0, schaechte)
            .Select(i => new DossierShaftLine(Guid.NewGuid(), $"S-{i}", "", "ohne", 0m))
            .ToList();

        var project = new Project();

        return new DossierSnapshot(
            Guid.NewGuid(),
            "Musterweg 1",
            holdings,
            Array.Empty<Guid>(),
            DashboardStatisticsBuilder.Build(Array.Empty<HaltungRecord>()),
            shafts);
    }

    [Fact]
    public void Die_Zeile_nennt_Leitungen_Schaechte_und_Laenge()
    {
        var text = DossiersPageViewModel.BuildSummaryLine(
            Stand(leitungen: 5, schaechte: 4, laenge: 28.4));

        Assert.Contains("5 Leitungen", text, StringComparison.Ordinal);
        Assert.Contains("4 Schächte", text, StringComparison.Ordinal);
        Assert.Contains("28.40 m", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Einzahl_bleibt_Einzahl()
    {
        var text = DossiersPageViewModel.BuildSummaryLine(Stand(leitungen: 1, schaechte: 1));

        Assert.Contains("1 Leitung ", text + " ", StringComparison.Ordinal);
        Assert.DoesNotContain("1 Leitungen", text, StringComparison.Ordinal);
        Assert.Contains("1 Schacht", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1 Schächte", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Schaechte_steht_dort_nichts()
    {
        // Eine „0 Schächte" wäre Rauschen; die meisten Dossiers haben keine.
        var text = DossiersPageViewModel.BuildSummaryLine(Stand(leitungen: 3));

        Assert.DoesNotContain("Schächte", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Schacht", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Laenge_und_ohne_Kosten_bleiben_beide_weg()
    {
        var text = DossiersPageViewModel.BuildSummaryLine(Stand(leitungen: 2));

        Assert.Equal("2 Leitungen", text);
    }

    [Fact]
    public void Kosten_erscheinen_erst_ab_einem_Betrag()
    {
        var text = DossiersPageViewModel.BuildSummaryLine(
            Stand(leitungen: 2, laenge: 10, kosten: 4500m));

        Assert.Contains("CHF", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_leeres_Dossier_sagt_das_auch()
    {
        Assert.Equal("Noch nichts zugeordnet", DossiersPageViewModel.BuildSummaryLine(Stand()));
    }
}

/// <summary>
/// Die Spalte „Empfohlene Massnahme" belegt die halbe Tabellenbreite — auch
/// wenn sie in jeder Zeile leer ist. Dann gehört der Platz dem Bauteilnamen.
/// </summary>
public sealed class DossierMeasureColumnTests
{
    [Fact]
    public void Ohne_jeden_Text_bleibt_die_Spalte_schmal()
        => Assert.False(DossierMeasureColumn.HasContent(new[] { "", "", "" }));

    [Fact]
    public void Ein_Strich_ist_kein_Text()
    {
        // Die Schachtzeilen tragen „—" statt einer leeren Zelle. Zählte das
        // als Inhalt, bliebe die Spalte immer breit.
        Assert.False(DossierMeasureColumn.HasContent(new[] { "—", "—" }));
    }

    [Fact]
    public void Ein_einziger_Eintrag_genuegt()
        => Assert.True(DossierMeasureColumn.HasContent(new[] { "—", "Schachthals sanieren", "" }));

    [Fact]
    public void Leerraum_zaehlt_nicht()
        => Assert.False(DossierMeasureColumn.HasContent(new[] { "   ", "\t" }));

    [Fact]
    public void Eine_leere_Tabelle_laesst_die_Spalte_schmal()
        => Assert.False(DossierMeasureColumn.HasContent(Array.Empty<string>()));

    [Fact]
    public void Fehlende_Liste_ist_kein_Absturz()
        => Assert.False(DossierMeasureColumn.HasContent(null));
}

/// <summary>Der Kopfblock der Seite lässt sich zuklappen.</summary>
public sealed class DossiersPageCollapsibleHeaderTests
{
    private static string Seite() => File.ReadAllText(RepoFile(
        "src", "AuswertungPro.Next.UI", "Views", "Pages", "DossiersPage.xaml"));

    [Fact]
    public void Die_Seite_bietet_den_Umschalter_an()
    {
        Assert.Contains("IsSummaryCollapsed", Seite(), StringComparison.Ordinal);
    }

    [Fact]
    public void Die_zugeklappte_Zeile_haengt_am_Zusammenzug()
    {
        Assert.Contains("Binding SummaryLine", Seite(), StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Zustand_wird_gemerkt()
    {
        // Einmal zuklappen soll reichen — nicht bei jedem Seitenwechsel erneut.
        var quelle = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DossiersPageViewModel.cs"));

        Assert.Contains("DossierSummaryCollapsed", quelle, StringComparison.Ordinal);
        Assert.Contains("_settings.Save()", quelle, StringComparison.Ordinal);
    }
}
