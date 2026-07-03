using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Schadensband der Haltungsansicht: Protokolleintraege → Band-Marker
/// (Meter, Farbe nach Code-Gruppe, Streckenschaeden als Balken).
/// </summary>
public sealed class HaltungSchadensbandBuilderTests
{
    private static HaltungRecord Haltung(string laenge = "", params ProtocolEntry[] entries)
    {
        var record = new HaltungRecord();
        if (laenge.Length > 0)
            record.SetFieldValue("Haltungslaenge_m", laenge, FieldSource.Xtf, userEdited: false);
        record.Protocol = new ProtocolDocument();
        foreach (var e in entries)
            record.Protocol.Current.Entries.Add(e);
        return record;
    }

    private static ProtocolEntry Eintrag(string code, double meter, double? ende = null, string text = "")
        => new()
        {
            Source = ProtocolEntrySource.Imported,
            Code = code,
            Beschreibung = text,
            MeterStart = meter,
            MeterEnd = ende ?? meter,
            IsStreckenschaden = ende.HasValue && ende.Value > meter
        };

    [Fact]
    public void Build_NimmtLaengeAusFeld_UndSortiertNachMeter()
    {
        var daten = HaltungSchadensbandBuilder.Build(Haltung("30.4",
            Eintrag("BBBA", 20.5),
            Eintrag("BCD", 0.0)));

        Assert.Equal(30.4, daten.TotalLength, 3);
        Assert.Equal(new[] { 0.0, 20.5 }, daten.Marker.Select(m => m.Meter));
    }

    [Fact]
    public void Build_FaelltAufMaxMeterZurueck_WennLaengeFehltOderNull()
    {
        var daten = HaltungSchadensbandBuilder.Build(Haltung("0",
            Eintrag("BCD", 0.0),
            Eintrag("BCE", 31.4)));

        Assert.Equal(31.4, daten.TotalLength, 3);
    }

    [Fact]
    public void Build_FarbenNachCodeGruppe()
    {
        var daten = HaltungSchadensbandBuilder.Build(Haltung("50",
            Eintrag("BAB", 1),    // strukturell -> rot
            Eintrag("BBBA", 2),   // betrieblich -> gelb
            Eintrag("BCA", 3),    // Grundgeruest -> gruen
            Eintrag("BDA", 4)));  // Sonstiges -> neutral

        Assert.Equal(MarkerColorKind.Red, daten.Marker[0].Farbe);
        Assert.Equal(MarkerColorKind.Yellow, daten.Marker[1].Farbe);
        Assert.Equal(MarkerColorKind.Green, daten.Marker[2].Farbe);
        Assert.Equal(MarkerColorKind.Rejected, daten.Marker[3].Farbe);
    }

    [Fact]
    public void Build_StreckenschadenLiefertMeterEnd()
    {
        var daten = HaltungSchadensbandBuilder.Build(Haltung("50",
            Eintrag("BAF", 2.5, ende: 8.0),
            Eintrag("BAB", 12.0)));

        Assert.Equal(8.0, daten.Marker[0].MeterEnd);
        Assert.Null(daten.Marker[1].MeterEnd); // Punktschaden: kein Ende
    }

    [Fact]
    public void Build_IgnoriertEintraegeOhneMeter_UndNullRecord()
    {
        var ohneMeter = new ProtocolEntry { Code = "BAB", Source = ProtocolEntrySource.Imported };
        var daten = HaltungSchadensbandBuilder.Build(Haltung("10", ohneMeter));

        Assert.Empty(daten.Marker);
        Assert.Empty(HaltungSchadensbandBuilder.Build(null).Marker);
    }
}
