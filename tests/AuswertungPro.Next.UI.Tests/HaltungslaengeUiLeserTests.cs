using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Deepscan 02.10.2026, A4: Auch die Leser der Haltungslaenge in der Oberflaeche deuten den
/// Feldwert nach <c>HaltungFeldwerte.LiesLaenge</c> (Regel des FachzahlParsers). Beispiele wie
/// in <c>HaltungFeldwerteTests.Laengenbeispiele</c> (Infrastructure.Tests).
/// </summary>
public sealed class HaltungslaengeUiLeserTests
{
    public static TheoryData<string, double?> Laengenbeispiele => new()
    {
        { "45.30", 45.3 },
        { "45,30", 45.3 },
        { "1'234.5", 1234.5 },
        { "1.234,5", 1234.5 },
        { "1,234.5", 1234.5 },
        { "-3", -3d },
        { "45 m", null },
        { "45 30", null },
        { "1e2", null },
        { "NaN", null },
        { "", null },
    };

    [Theory]
    [MemberData(nameof(Laengenbeispiele))]
    public void Videoueberlagerung_liest_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
    {
        var haltung = Haltung(roh);
        haltung.Protocol = new ProtocolDocument
        {
            HaltungId = "H1",
            Current = new ProtocolRevision { Entries = [new() { Code = "BAB", MeterStart = 1.0 }] }
        };

        Assert.Equal(Positiv(erwartet), DataPageVideoOverlayBuilder.Build(haltung)?.PipeLengthMeters);
    }

    [Theory]
    [MemberData(nameof(Laengenbeispiele))]
    public void Sanierungsoptimierung_liest_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
        => Assert.Equal(Positiv(erwartet), SanierungOptimizationViewModel.BuildRequest(Haltung(roh), null).Pipe.LengthMeter);

    [Theory]
    [MemberData(nameof(Laengenbeispiele))]
    public void Codiermodus_liest_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
    {
        var haltung = Haltung(roh);

        Assert.Equal(Positiv(erwartet), CodingHaltungslaengeResolver.TryReadHaltungslaenge(haltung));
        Assert.Equal(Positiv(erwartet) is not null, CodingHaltungslaengeResolver.HasValidLength(haltung, FieldKeys.HoldingLengthMeters));
    }

    [Theory]
    [MemberData(nameof(Laengenbeispiele))]
    public void Dossierauswahl_zeigt_die_Haltungslaenge_nach_der_gemeinsamen_Regel(string roh, double? erwartet)
    {
        var anzeige = Positiv(erwartet) is { } laenge
            ? laenge.ToString("0.0", CultureInfo.GetCultureInfo("de-CH")) + " m"
            : "—";

        Assert.Equal(anzeige, new DossierHoldingChoice(Haltung(roh), chosen: false).Length);
    }

    private static double? Positiv(double? wert) => wert is > 0 ? wert : null;

    private static HaltungRecord Haltung(string laenge)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "H1", FieldSource.Manual, userEdited: false);
        record.SetFieldValue(FieldKeys.HoldingLengthMeters, laenge, FieldSource.Manual, userEdited: false);
        return record;
    }
}
