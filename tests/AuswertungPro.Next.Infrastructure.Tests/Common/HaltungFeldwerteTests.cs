using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

/// <summary>
/// Die gemeinsame Leseregel fuer Inspektionsdatum und Haltungslaenge (Deepscan 02.10.2026, A4).
/// Die Beispieltabellen sind oeffentlich, damit die Tests der umgestellten Leser (Mediensuche,
/// Dateistempel, Training, Bewertung, Dashboard, Dossier ...) dieselben Faelle pruefen.
/// </summary>
public sealed class HaltungFeldwerteTests
{
    /// <summary>Rohwert, erwartetes Datum (ISO) oder null, nur Jahr bekannt.</summary>
    public static TheoryData<string, string?, bool> Datumsbeispiele => new()
    {
        { "05.03.2024", "2024-03-05", false },
        { "5.3.2024", "2024-03-05", false },
        { "24.09.25", "2025-09-24", false },
        { "2024-03-05", "2024-03-05", false },
        { "2024.03.05", "2024-03-05", false },
        { "20240305", "2024-03-05", false },
        { "2024-03-05T10:00:00", "2024-03-05", false },
        { "05.03.2024 14:30", "2024-03-05", false },
        { "Aufnahmen: 04.12.14 - 05.12.14", "2014-12-04", false },
        { "20251110_9866-9327.pdf", "2025-11-10", false },
        { "2024", "2024-01-01", true },
        { "GEP Aufnahmen Altdorf 2025", "2025-01-01", true },
        { "", null, false },
        { "unbekannt", null, false },
        { "24/25", null, false },
        { "5.3", null, false },
    };

    /// <summary>Rohwert, erwartete Laenge in Metern oder null.</summary>
    public static TheoryData<string, double?> Laengenbeispiele => new()
    {
        { "45.30", 45.3 },
        { "45,30", 45.3 },
        { " 12.5 ", 12.5 },
        { "1'234.5", 1234.5 },
        { "1.234,5", 1234.5 },
        { "1,234.5", 1234.5 },
        { "1.300", 1.3 },
        { "-3", -3d },
        { "45 m", null },
        { "45 30", null },
        { "1e2", null },
        { "NaN", null },
        { "", null },
    };

    [Theory]
    [MemberData(nameof(Datumsbeispiele))]
    public void LiesInspektionsdatum_deutet_jeden_Wert_nach_einer_Regel(string roh, string? erwartetIso, bool nurJahr)
    {
        var gelesen = HaltungFeldwerte.LiesInspektionsdatumGenau(roh);

        if (erwartetIso is null)
        {
            Assert.Null(gelesen);
            Assert.Null(HaltungFeldwerte.LiesInspektionsdatum(roh));
            return;
        }

        Assert.NotNull(gelesen);
        Assert.Equal(Iso(erwartetIso), gelesen.Value.Datum);
        Assert.Equal(nurJahr, gelesen.Value.NurJahr);
        Assert.Equal(Iso(erwartetIso), HaltungFeldwerte.LiesInspektionsdatum(roh));
    }

    [Theory]
    [InlineData("de-CH")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void LiesInspektionsdatum_haengt_nicht_von_der_Windows_Kultur_ab(string kultur)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(kultur);
            Assert.Equal(Iso("2024-03-05"), HaltungFeldwerte.LiesInspektionsdatum("5.3.2024"));
            Assert.Equal(Iso("2025-09-24"), HaltungFeldwerte.LiesInspektionsdatum("24.09.25"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void LiesInspektionsdatum_ungueltiger_Kalendertag_faellt_auf_das_Jahr_zurueck()
    {
        var gelesen = HaltungFeldwerte.LiesInspektionsdatumGenau("31.02.2024");

        Assert.Equal(new Inspektionsdatum(Iso("2024-01-01"), NurJahr: true), gelesen);
    }

    [Fact]
    public void LiesInspektionsdatum_liest_das_Feld_Datum_Jahr_der_Haltung()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.InspectionYear, "5.3.2024", FieldSource.Manual, userEdited: true);

        Assert.Equal(Iso("2024-03-05"), HaltungFeldwerte.LiesInspektionsdatum(record));
    }

    [Theory]
    [MemberData(nameof(Laengenbeispiele))]
    public void LiesLaenge_folgt_dem_FachzahlParser(string roh, double? erwartet)
    {
        Assert.Equal(erwartet, HaltungFeldwerte.LiesLaenge(roh));
    }

    [Fact]
    public void LiesLaenge_liest_das_Feld_Haltungslaenge_der_Haltung()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingLengthMeters, "1'234,5", FieldSource.Manual, userEdited: true);

        Assert.Equal(1234.5, HaltungFeldwerte.LiesLaenge(record));
    }

    internal static DateTime Iso(string iso)
        => DateTime.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
