using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Application.Schatten;
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Vsa;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

/// <summary>
/// Deepscan 02.10.2026, A4: Alle Leser der Haltungslaenge deuten den Feldwert nach
/// <c>HaltungFeldwerte.LiesLaenge</c> (Regel des FachzahlParsers). Jeder Leser behaelt nur
/// seine eigene Folge fuer Unlesbares (0, null oder keine Pruefung) und fuer Werte &lt;= 0.
/// Vorher: Bewertung, Kostenfall, Uebersicht, Schatten, Protokoll-PDF, Plausibilitaet,
/// Reichweite und Codiersitzung lasen "1'234.5" als unlesbar und "1e2" als 100;
/// Dashboard und Dossier lasen "45 30" als 4530.
/// </summary>
public sealed class HaltungslaengeLeserTests
{
    [Theory]
    [MemberData(nameof(HaltungFeldwerteTests.Laengenbeispiele), MemberType = typeof(HaltungFeldwerteTests))]
    public void Bewertung_liest_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
        => Assert.Equal(erwartet ?? 0d, VsaEvaluationService.LiesBewertungslaenge(Haltung(roh)));

    [Theory]
    [MemberData(nameof(HaltungFeldwerteTests.Laengenbeispiele), MemberType = typeof(HaltungFeldwerteTests))]
    public void Dashboard_summiert_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
        => Assert.Equal(Math.Round(erwartet ?? 0d, 2), DashboardStatisticsBuilder.Build([Haltung(roh)]).TotalLengthMeters);

    [Theory]
    [MemberData(nameof(HaltungFeldwerteTests.Laengenbeispiele), MemberType = typeof(HaltungFeldwerteTests))]
    public void Dossier_liest_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
    {
        var haltung = Haltung(roh);
        var projekt = new Project();
        projekt.Data.Add(haltung);

        var snapshot = DossierSnapshotBuilder.Build(new DossierDefinition { HoldingIds = [haltung.Id] }, projekt, null);

        Assert.Equal(erwartet, Assert.Single(snapshot.Holdings).LengthMeters);
    }

    [Theory]
    [MemberData(nameof(HaltungFeldwerteTests.Laengenbeispiele), MemberType = typeof(HaltungFeldwerteTests))]
    public void Kostenfall_liest_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
        => Assert.Equal(erwartet ?? 0d, KostenfallMerkmalLeser.Lies(Haltung(roh)).LaengeM);

    [Theory]
    [MemberData(nameof(HaltungFeldwerteTests.Laengenbeispiele), MemberType = typeof(HaltungFeldwerteTests))]
    public void Uebersicht_summiert_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
    {
        var projekt = new Project();
        projekt.Data.Add(Haltung(roh));

        Assert.Equal(Positiv(erwartet) ?? 0d, ProjektUebersichtRechner.Berechne(projekt).GesamtlaengeM);
    }

    [Theory]
    [MemberData(nameof(HaltungFeldwerteTests.Laengenbeispiele), MemberType = typeof(HaltungFeldwerteTests))]
    public void Schattenanfrage_liest_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
        => Assert.Equal(Positiv(erwartet), SchattenLlmRequestBuilder.Build(Haltung(roh), null, null).Pipe.LengthMeter);

    [Theory]
    [MemberData(nameof(HaltungFeldwerteTests.Laengenbeispiele), MemberType = typeof(HaltungFeldwerteTests))]
    public void Protokoll_PDF_liest_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
        => Assert.Equal(Positiv(erwartet), ProtocolPdfEntryResolver.ResolveHoldingLength(Haltung(roh), []));

    [Theory]
    [MemberData(nameof(HaltungFeldwerteTests.Laengenbeispiele), MemberType = typeof(HaltungFeldwerteTests))]
    public void Plausibilitaet_prueft_gegen_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
    {
        var haltung = Haltung(roh);
        var hinterDemEnde = (Positiv(erwartet) ?? 10d) + 5d;
        haltung.Protocol = new ProtocolDocument
        {
            HaltungId = "H1",
            Current = new ProtocolRevision { Entries = [new() { Code = "BAB", MeterStart = hinterDemEnde }] }
        };

        var warnt = ImportPlausibilityValidator.Validate(haltung).Any(w => w.Contains("hinter der Haltungslaenge", StringComparison.Ordinal));

        Assert.Equal(Positiv(erwartet) is not null, warnt);
    }

    [Theory]
    [MemberData(nameof(HaltungFeldwerteTests.Laengenbeispiele), MemberType = typeof(HaltungFeldwerteTests))]
    public void Reichweite_der_Videoanalyse_liest_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
        => Assert.Equal(Positiv(erwartet), PipelineReachLengthParser.TryParse(roh));

    [Theory]
    [MemberData(nameof(HaltungFeldwerteTests.Laengenbeispiele), MemberType = typeof(HaltungFeldwerteTests))]
    public void Codiersitzung_liest_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
        => Assert.Equal(Positiv(erwartet), PrimaryDamageLineParser.TryParseLengthField(Haltung(roh), FieldKeys.HoldingLengthMeters));

    private static double? Positiv(double? wert) => wert is > 0 ? wert : null;

    private static HaltungRecord Haltung(string laenge)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "H1", FieldSource.Manual, userEdited: false);
        record.SetFieldValue(FieldKeys.HoldingLengthMeters, laenge, FieldSource.Manual, userEdited: false);
        return record;
    }
}
