using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

/// <summary>
/// "Abgleichen": In Haltungen_Verteilt und Schaechte_Verteilt soll nur liegen, wozu es
/// im Projekt eine Haltung bzw. einen Schacht gibt. Alles andere wandert in den
/// Papierkorb des Projekts - verschoben, nie geloescht.
/// </summary>
public sealed class DistributionReconciliationServiceTests : IDisposable
{
    private static readonly DateTime Zeitpunkt = new(2026, 8, 21, 14, 30, 0, DateTimeKind.Local);

    private readonly string _projekt = Path.Combine(
        Path.GetTempPath(), "abgl_" + Guid.NewGuid().ToString("N"));

    public DistributionReconciliationServiceTests() => Directory.CreateDirectory(_projekt);

    private string Verteilt(string wurzel, string unterordner, string datei = "inhalt.pdf")
    {
        var ordner = Path.Combine(_projekt, wurzel, unterordner);
        Directory.CreateDirectory(ordner);
        var pfad = Path.Combine(ordner, datei);
        File.WriteAllText(pfad, "x");
        return pfad;
    }

    private static Project ProjektMit(string[] haltungen, string[] schaechte)
    {
        var project = new Project();
        foreach (var name in haltungen)
        {
            var r = new HaltungRecord();
            r.SetFieldValue("Haltungsname", name, FieldSource.Manual, false);
            project.Data.Add(r);
        }

        foreach (var nummer in schaechte)
        {
            var s = new SchachtRecord();
            s.SetFieldValue("Schachtnummer", nummer);
            project.SchaechteData.Add(s);
        }

        return project;
    }

    private DistributionReconciliationResult Abgleichen(Project project)
    {
        var dienst = new DistributionReconciliationService();
        var plan = dienst.Plan(_projekt, project);
        return dienst.Apply(_projekt, plan, Zeitpunkt);
    }

    private string PapierkorbLauf => Path.Combine(_projekt, "Papierkorb", "2026-08-21_143000");

    [Fact]
    public void Ordner_ohne_haltung_wandert_in_den_papierkorb()
    {
        var waise = Verteilt("Haltungen_Verteilt", "99999-88888");
        var bekannt = Verteilt("Haltungen_Verteilt", "80707-80713");

        var ergebnis = Abgleichen(ProjektMit(["80707-80713"], []));

        Assert.False(Directory.Exists(Path.GetDirectoryName(waise)!));
        Assert.True(File.Exists(bekannt));
        Assert.True(File.Exists(Path.Combine(
            PapierkorbLauf, "Haltungen_Verteilt", "99999-88888", "inhalt.pdf")));
        Assert.Equal(1, ergebnis.MovedDirectories);
    }

    [Fact]
    public void Vertauschte_schachtreihenfolge_gilt_als_zugeordnet()
    {
        // Die Verteilung selbst sieht A-B und B-A als denselben Treffer. Wer das hier
        // anders bewertet, raeumt Ordner weg, die eigentlich verknuepft sind.
        var vertauscht = Verteilt("Haltungen_Verteilt", "80713-80707");

        var ergebnis = Abgleichen(ProjektMit(["80707-80713"], []));

        Assert.True(File.Exists(vertauscht));
        Assert.Equal(0, ergebnis.MovedDirectories);
    }

    [Fact]
    public void Schachtordner_wird_nach_derselben_regel_behandelt()
    {
        var waise = Verteilt("Schächte_Verteilt", "80374");
        var bekannt = Verteilt("Schächte_Verteilt", "80707");

        Abgleichen(ProjektMit([], ["80707"]));

        Assert.False(Directory.Exists(Path.GetDirectoryName(waise)!));
        Assert.True(File.Exists(bekannt));
        Assert.True(File.Exists(Path.Combine(
            PapierkorbLauf, "Schächte_Verteilt", "80374", "inhalt.pdf")));
    }

    [Fact]
    public void Lose_datei_in_der_wurzel_wandert_ebenfalls()
    {
        var wurzel = Path.Combine(_projekt, "Haltungen_Verteilt");
        Directory.CreateDirectory(wurzel);
        var lose = Path.Combine(wurzel, "irgendwas.pdf");
        File.WriteAllText(lose, "x");

        var ergebnis = Abgleichen(ProjektMit(["80707-80713"], []));

        Assert.False(File.Exists(lose));
        Assert.True(File.Exists(Path.Combine(PapierkorbLauf, "Haltungen_Verteilt", "irgendwas.pdf")));
        Assert.Equal(1, ergebnis.MovedFiles);
    }

    [Fact]
    public void Leeres_projekt_sperrt_den_abgleich()
    {
        // Sonst raeumt ein versehentlich leeres Projekt beide Ordner komplett aus.
        var vorhanden = Verteilt("Haltungen_Verteilt", "80707-80713");

        var dienst = new DistributionReconciliationService();
        var plan = dienst.Plan(_projekt, new Project());

        Assert.False(string.IsNullOrWhiteSpace(plan.BlockedReason));
        Assert.Empty(plan.ToMove);

        var ergebnis = dienst.Apply(_projekt, plan, Zeitpunkt);

        Assert.True(File.Exists(vorhanden));
        Assert.Equal(0, ergebnis.MovedDirectories);
        Assert.False(Directory.Exists(Path.Combine(_projekt, "Papierkorb")));
    }

    [Fact]
    public void Papierkorb_wird_nicht_selbst_abgeglichen()
    {
        var imPapierkorb = Path.Combine(_projekt, "Papierkorb", "alt", "Haltungen_Verteilt", "1-2");
        Directory.CreateDirectory(imPapierkorb);
        File.WriteAllText(Path.Combine(imPapierkorb, "alt.pdf"), "x");
        Verteilt("Haltungen_Verteilt", "80707-80713");

        Abgleichen(ProjektMit(["80707-80713"], []));

        Assert.True(File.Exists(Path.Combine(imPapierkorb, "alt.pdf")));
    }

    [Fact]
    public void Zweiter_lauf_bekommt_einen_eigenen_zeitstempel()
    {
        Verteilt("Haltungen_Verteilt", "11111-22222");
        var dienst = new DistributionReconciliationService();
        var project = ProjektMit(["80707-80713"], []);

        dienst.Apply(_projekt, dienst.Plan(_projekt, project), Zeitpunkt);
        Verteilt("Haltungen_Verteilt", "11111-22222");
        dienst.Apply(_projekt, dienst.Plan(_projekt, project), Zeitpunkt.AddMinutes(5));

        Assert.True(File.Exists(Path.Combine(
            PapierkorbLauf, "Haltungen_Verteilt", "11111-22222", "inhalt.pdf")));
        Assert.True(File.Exists(Path.Combine(
            _projekt, "Papierkorb", "2026-08-21_143500", "Haltungen_Verteilt", "11111-22222", "inhalt.pdf")));
    }

    [Fact]
    public void Plan_meldet_die_waisen_bevor_etwas_bewegt_wird()
    {
        var waise = Verteilt("Haltungen_Verteilt", "99999-88888");

        var plan = new DistributionReconciliationService().Plan(_projekt, ProjektMit(["80707-80713"], []));

        Assert.Contains(plan.ToMove, e => e.RelativePath.Contains("99999-88888", StringComparison.Ordinal));
        // Der Plan allein veraendert nichts.
        Assert.True(File.Exists(waise));
    }

    public void Dispose()
    {
        try { Directory.Delete(_projekt, recursive: true); } catch { }
    }
}
