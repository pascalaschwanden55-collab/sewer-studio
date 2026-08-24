using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die Schachtzeile im Word-Dokument. Sie muss dasselbe sagen wie die Tabelle
/// im Cockpit — ein Eigentuemer, der andere Angaben liest als die Sachbearbeitung
/// vor sich hat, ist der schlimmste Fall.
/// </summary>
public sealed class DossierWordShaftLineTests
{
    private static string Bauteilliste(DossierShaftLine schacht)
    {
        var dossier = new DossierDefinition { Name = "Musterweg 1" };

        var snapshot = new DossierSnapshot(
            dossier.Id,
            dossier.Name,
            Array.Empty<DossierHoldingLine>(),
            Array.Empty<Guid>(),
            DashboardStatisticsBuilder.Build(Array.Empty<HaltungRecord>()),
            new[] { schacht });

        var werte = DossierWordTemplateExportService.BuildValues(new DossierExportRequest(
            new Project(),
            Path.GetTempPath(),
            new DossierAreaSettings(),
            dossier,
            snapshot,
            Path.GetTempPath()));

        return werte[DossierTopicComponentListComposer.ValueKey];
    }

    [Fact]
    public void Die_Schachtzeile_nennt_die_Funktion()
    {
        var text = Bauteilliste(new DossierShaftLine(
            Guid.NewGuid(), "80551", "", "ohne", 0m, "Kontrollschacht", ""));

        Assert.Contains("Kontrollschacht", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Schachtzeile_nennt_die_empfohlene_Massnahme()
    {
        var text = Bauteilliste(new DossierShaftLine(
            Guid.NewGuid(), "80551", "", "ohne", 0m, "", "Schachthals sanieren"));

        Assert.Contains("Schachthals sanieren", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Schachtzeile_nennt_die_Kosten()
    {
        var text = Bauteilliste(new DossierShaftLine(
            Guid.NewGuid(), "80551", "", "ohne", 1100m, "", ""));

        Assert.Contains("1", text, StringComparison.Ordinal);
        Assert.Contains("100", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Angaben_bleibt_die_nackte_Nummer_stehen()
    {
        // Kein "—" und kein "0.00" im Brief an den Eigentuemer.
        var text = Bauteilliste(new DossierShaftLine(
            Guid.NewGuid(), "80551", "", "ohne", 0m));

        Assert.Equal("1. Schacht 80551", text);
    }
}

/// <summary>
/// Das Cockpit und die Ausgabe muessen denselben Stand rechnen.
/// </summary>
public sealed class DossierSnapshotSingleSourceTests
{
    [Fact]
    public void Die_Dossierseite_baut_den_Stand_an_genau_einer_Stelle()
    {
        // Zwei Aufbaustellen sind in diesem Programm schon einmal
        // auseinandergelaufen: das Cockpit zeigte Schachtkosten, die im Word
        // fehlten, weil die zweite Stelle die Kostendatei nicht mitgab.
        var quellen = new[]
        {
            "DossiersPageViewModel.cs",
            "DossiersPageViewModel.Actions.cs"
        };

        var treffer = quellen
            .Select(name => Path.Combine(
                RepoRoot(), "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", name))
            .Select(File.ReadAllText)
            .Sum(text => Vorkommen(text, "DossierSnapshotBuilder.Build("));

        Assert.Equal(1, treffer);
    }

    private static int Vorkommen(string text, string suche)
    {
        var anzahl = 0;
        var stelle = 0;
        while ((stelle = text.IndexOf(suche, stelle, StringComparison.Ordinal)) >= 0)
        {
            anzahl++;
            stelle += suche.Length;
        }

        return anzahl;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AuswertungPro.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
