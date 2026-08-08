using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Sequenz-Plausibilitaet der Meterstaende. Die Einzelbild-Lesung ist
/// zustandslos; ob ein Wert moeglich ist, entscheidet erst die Folge.
///
/// Belegt am 2026-08-08: Der Leser meldete 133,08 m in einer Haltung von keinen
/// 20 m. Die Zahlenform allein reicht als Pruefung nicht.
///
/// Portiert aus `plausibilisiere_sequenz` in training/scripts/osd_meter_leser.py.
/// Python liefert Rohwerte, C# entscheidet — so will es die Thin-AI-Regel.
/// </summary>
public sealed class MeterSequencePlausibilityTests
{
    [Fact]
    public void Ein_unmoeglich_hoher_Wert_wird_verworfen()
    {
        var geprueft = Pruefe((10, 3.0), (11, 133.08), (12, 3.2), (13, 3.4));

        Assert.Equal(new double?[] { 3.0, null, 3.2, 3.4 }, geprueft);
    }

    [Fact]
    public void Ein_Wert_der_zu_keinem_Nachbarn_passt_wird_verworfen()
    {
        // Die Kamera faehrt keine 20 m in einer Sekunde.
        var geprueft = Pruefe((10, 3.0), (11, 25.0), (12, 3.2));

        Assert.Equal(new double?[] { 3.0, null, 3.2 }, geprueft);
    }

    [Fact]
    public void Ein_zuegiger_aber_moeglicher_Fortschritt_bleibt_erhalten()
    {
        // 5 m je Sekunde ist die Grenze; 3 m in einer Sekunde sind erlaubt.
        var geprueft = Pruefe((10, 3.0), (11, 6.0), (12, 9.0));

        Assert.Equal(new double?[] { 3.0, 6.0, 9.0 }, geprueft);
    }

    [Fact]
    public void Ohne_zeitnahen_Nachbarn_wird_nichts_verworfen()
    {
        // Ein einzelner Wert weit weg von allen anderen ist unbelegt, nicht falsch.
        var geprueft = Pruefe((10, 3.0), (11, 3.2), (400, 12.0));

        Assert.Equal(new double?[] { 3.0, 3.2, 12.0 }, geprueft);
    }

    [Fact]
    public void Eine_Rueckwaertsfahrt_ist_kein_Fehler()
    {
        var geprueft = Pruefe((10, 7.4), (11, 7.0), (12, 6.6));

        Assert.Equal(new double?[] { 7.4, 7.0, 6.6 }, geprueft);
    }

    [Fact]
    public void Mit_weniger_als_zwei_Messungen_wird_nicht_geurteilt()
    {
        // Eine einzelne Lesung hat keinen Kontext — auch eine absurde bleibt stehen,
        // weil nichts sie widerlegt. Die Obergrenze des Aggregators faengt sie ab.
        var geprueft = Pruefe((10, 999.0));

        Assert.Equal(new double?[] { 999.0 }, geprueft);
    }

    [Fact]
    public void Unlesbare_Stellen_bleiben_unlesbar()
    {
        var geprueft = MeterSequencePlausibility.Check(
            [
                new MeterReading(10, 3.0),
                new MeterReading(11, null),
                new MeterReading(12, 3.2)
            ],
            new MeterPlausibilityOptions());

        Assert.Equal(new double?[] { 3.0, null, 3.2 }, geprueft.Select(r => r.Meter));
    }

    [Fact]
    public void Die_Zeitpunkte_bleiben_unveraendert()
    {
        var geprueft = MeterSequencePlausibility.Check(
            [new MeterReading(10, 3.0), new MeterReading(11, 133.08)],
            new MeterPlausibilityOptions());

        Assert.Equal(new[] { 10.0, 11.0 }, geprueft.Select(r => r.TimeSeconds));
    }

    [Fact]
    public void Ohne_Lesungen_kommt_nichts_zurueck()
    {
        Assert.Empty(MeterSequencePlausibility.Check(null, new MeterPlausibilityOptions()));
        Assert.Empty(MeterSequencePlausibility.Check([], new MeterPlausibilityOptions()));
    }

    private static IReadOnlyList<double?> Pruefe(params (double Zeit, double? Meter)[] werte)
        => MeterSequencePlausibility
            .Check(werte.Select(w => new MeterReading(w.Zeit, w.Meter)), new MeterPlausibilityOptions())
            .Select(r => r.Meter)
            .ToList();
}
