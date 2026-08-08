using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.BendSuggestions;

/// <summary>Grenzen der Sequenzpruefung.</summary>
public sealed record MeterPlausibilityOptions
{
    /// <summary>Groesste glaubwuerdige Kamerageschwindigkeit.</summary>
    public double MaxMetersPerSecond { get; init; } = 5.0;

    /// <summary>Zeitfenster, in dem ein Nachbar als Kontext zaehlt.</summary>
    public double NeighbourWindowSeconds { get; init; } = 10.0;

    /// <summary>
    /// Vielfaches des Medians als Obergrenze. Die Haltungslaenge ist hier nicht
    /// bekannt, deshalb bewusst grosszuegig.
    /// </summary>
    public double CeilingMedianFactor { get; init; } = 4.0;

    /// <summary>Untergrenze der Obergrenze, damit kurze Haltungen nicht zu streng werden.</summary>
    public double CeilingMinimumMeters { get; init; } = 30.0;
}

/// <summary>
/// Prueft eine Folge von Meterstaenden auf Moeglichkeit.
///
/// Die Einzelbild-Lesung ist zustandslos — ob ein Wert moeglich ist, entscheidet
/// erst die Folge. Belegt am 2026-08-08: Der OSD-Leser meldete 133,08 m in einer
/// Haltung von keinen 20 m, und die Zahlenform allein liess das durch.
///
/// Zwei Gruende, einen Wert zu verwerfen: Er liegt ueber der robusten Obergrenze
/// des Videos, oder er passt zu KEINEM zeitnahen Nachbarn — die Kamera faehrt
/// nicht 130 Meter in einer Sekunde. Ohne zeitnahen Nachbarn wird nichts
/// verworfen: unbelegt ist nicht falsch.
///
/// Portiert aus `plausibilisiere_sequenz` in training/scripts/osd_meter_leser.py.
/// Python liefert die Rohwerte, C# entscheidet — so verlangt es die Thin-AI-Regel.
/// </summary>
public static class MeterSequencePlausibility
{
    public static IReadOnlyList<MeterReading> Check(
        IEnumerable<MeterReading>? readings,
        MeterPlausibilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (readings is null)
            return Array.Empty<MeterReading>();

        var all = readings.Where(reading => reading is not null).ToList();
        var measured = all.Where(reading => reading.Meter.HasValue).ToList();
        if (measured.Count < 2)
            return all;

        var sorted = measured.Select(reading => reading.Meter!.Value).OrderBy(value => value).ToList();
        var median = sorted[sorted.Count / 2];
        var ceiling = Math.Max(options.CeilingMedianFactor * median, options.CeilingMinimumMeters);

        var suspicious = new HashSet<double>();
        foreach (var reading in measured)
        {
            if (IsSuspicious(reading, measured, ceiling, options))
                suspicious.Add(reading.TimeSeconds);
        }

        return all
            .Select(reading => suspicious.Contains(reading.TimeSeconds)
                ? reading with { Meter = null }
                : reading)
            .ToList();
    }

    private static bool IsSuspicious(
        MeterReading reading,
        List<MeterReading> measured,
        double ceiling,
        MeterPlausibilityOptions options)
    {
        var meter = reading.Meter!.Value;
        if (meter > ceiling)
            return true;

        var neighbours = measured
            .Where(other => !ReferenceEquals(other, reading))
            .Select(other => (
                gap: Math.Abs(reading.TimeSeconds - other.TimeSeconds),
                meter: other.Meter!.Value))
            .Where(pair => pair.gap > 0 && pair.gap <= options.NeighbourWindowSeconds)
            .ToList();

        // Ohne zeitnahen Kontext laesst sich nichts widerlegen — unbelegt ist nicht falsch.
        if (neighbours.Count == 0)
            return false;

        return neighbours.All(pair =>
            Math.Abs(meter - pair.meter) > options.MaxMetersPerSecond * pair.gap);
    }
}
