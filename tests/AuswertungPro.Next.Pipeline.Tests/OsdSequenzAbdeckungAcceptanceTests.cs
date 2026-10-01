using System.Text.Json;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Wie oft bekommt eine Stelle im Video am Ende einen Meterstand?
///
/// Die Archivmessung des Lesers zaehlt einzelne Bilder an weit auseinander
/// liegenden Stellen. So arbeitet das Programm nicht: Der Bogen-Scan zieht ein Bild
/// je Sekunde, und <see cref="MeterSequenceGapFiller"/> fuellt danach kurze Luecken.
/// Die fuer den Benutzer sichtbare Quote ist deshalb hoeher als die reine Lesequote.
///
/// Die Rohfolgen erzeugt <c>training/scripts/osd_sequenz_abdeckung.py</c> aus echten
/// Kundenvideos. Gefuellt wird hier mit dem PRODUKTIVEN Code — eine Nachbildung in
/// Python wuerde frueher oder spaeter abweichen, und genau darum geht es.
///
/// Ohne die erzeugte Datei wird der Test uebersprungen; er gehoert zum Messen, nicht
/// zum Absichern.
/// </summary>
public sealed class OsdSequenzAbdeckungAcceptanceTests
{
    internal const string FolgenPfad =
        @"C:\KI_BRAIN\training\diagnostics\osd_sequenz_abdeckung_20260814.json";

    private readonly ITestOutputHelper _out;

    public OsdSequenzAbdeckungAcceptanceTests(ITestOutputHelper output) => _out = output;

    [MeterfolgenFact]
    public void Wie_viele_Stellen_bekommen_nach_dem_Fuellen_einen_Meterstand()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(FolgenPfad));
        var optionen = new MeterGapFillOptions();

        var gesamtBilder = 0;
        var gesamtGelesen = 0;
        var gesamtMitWert = 0;

        _out.WriteLine($"{"Haltung",-18}{"Gr.",-5}{"Bilder",8}{"gelesen",10}{"mit Wert",11}");
        foreach (var video in doc.RootElement.GetProperty("videos").EnumerateArray())
        {
            if (video.GetProperty("zustand").GetString() != "geprueft")
                continue;

            var rohe = video.GetProperty("folge").EnumerateArray()
                .Select(f => new MeterReading(
                    f.GetProperty("t").GetDouble(),
                    f.GetProperty("meter").ValueKind == JsonValueKind.Null
                        ? null
                        : f.GetProperty("meter").GetDouble()))
                .ToList();

            var gefuellt = MeterSequenceGapFiller.Fill(rohe, optionen);

            var bilder = rohe.Count;
            var gelesen = rohe.Count(r => r.Meter is not null);
            var mitWert = gefuellt.Count(r => r.Meter is not null);

            gesamtBilder += bilder;
            gesamtGelesen += gelesen;
            gesamtMitWert += mitWert;

            _out.WriteLine(
                $"{video.GetProperty("haltung").GetString(),-18}"
                + $"{video.GetProperty("gruppe").GetString(),-5}{bilder,8}"
                + $"{gelesen,7} {(double)gelesen / bilder,6:P0}"
                + $"{mitWert,8} {(double)mitWert / bilder,6:P0}");
        }

        Assert.True(gesamtBilder > 0, "Keine geprueften Videos in der Datei.");

        _out.WriteLine(new string('-', 52));
        _out.WriteLine($"{"GESAMT",-23}{gesamtBilder,8}"
            + $"{gesamtGelesen,7} {(double)gesamtGelesen / gesamtBilder,6:P0}"
            + $"{gesamtMitWert,8} {(double)gesamtMitWert / gesamtBilder,6:P0}");

        // Das Fuellen darf nie Werte entfernen.
        Assert.True(gesamtMitWert >= gesamtGelesen);
    }
}

/// <summary>Laeuft nur, wenn die erzeugten Meterfolgen vorliegen.</summary>
public sealed class MeterfolgenFactAttribute : FactAttribute
{
    public MeterfolgenFactAttribute()
    {
        if (!File.Exists(OsdSequenzAbdeckungAcceptanceTests.FolgenPfad))
            Skip = "Meterfolgen nicht vorhanden — training/scripts/osd_sequenz_abdeckung.py ausfuehren.";
    }
}
