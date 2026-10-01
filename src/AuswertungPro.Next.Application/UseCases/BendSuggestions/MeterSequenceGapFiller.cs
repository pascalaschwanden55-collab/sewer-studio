using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.BendSuggestions;

/// <summary>Ein Meterstand zu einem Zeitpunkt. Null = nicht lesbar.</summary>
public sealed record MeterReading(double TimeSeconds, double? Meter);

/// <summary>Ein Meterstand nach dem Fuellen kurzer Luecken.</summary>
public sealed record FilledMeterReading(double TimeSeconds, double? Meter, bool IsEstimated);

/// <summary>Grenzen des Fuellens.</summary>
public sealed record MeterGapFillOptions
{
    /// <summary>
    /// Groesster Abstand zwischen den beiden gelesenen Klammerwerten. Darueber
    /// wird nicht gefuellt: Bei schlechter Lesequote sind die Luecken keine
    /// Luecken mehr, sondern Wuesten.
    /// </summary>
    public double MaxGapSeconds { get; init; } = 10.0;
}

/// <summary>
/// Fuellt kurze Luecken zwischen zwei gelesenen Meterstaenden.
///
/// Der OSD-Leser deckt je nach Anzeigestil 17 bis 95 Prozent der Bilder ab.
/// Kurze Luecken sind der Normalfall und kosten sonst Zuordnung: Ein Bild ohne
/// Meterstand kann eine Stelle nicht mitbestimmen.
///
/// Drei harte Klammern, jede aus einem echten Fehler geboren:
/// 1. Nur zwischen GELESENEN Werten — eine Schaetzung darf nie selbst Klammer
///    sein, sonst wandert sie schrittweise durch das ganze Video.
/// 2. Nur ueber kurze Luecken.
/// 3. Nie ueber einen Richtungswechsel. Faellt der Meterstand zwischen zwei
///    Messungen, ist die Kamera zurueckgefahren; ein interpolierter Wert saehe
///    sauber aus und waere falsch. Dieselbe Fehlerklasse wie beim Glaetten,
///    nur lokaler.
///
/// Gefuellte Werte tragen <see cref="FilledMeterReading.IsEstimated"/> und
/// duerfen deshalb zuordnen, aber keinen Ort setzen.
/// </summary>
public static class MeterSequenceGapFiller
{
    public static IReadOnlyList<FilledMeterReading> Fill(
        IEnumerable<MeterReading>? readings,
        MeterGapFillOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (readings is null)
            return Array.Empty<FilledMeterReading>();

        var ordered = readings
            .Where(reading => reading is not null)
            .OrderBy(reading => reading.TimeSeconds)
            .ToList();
        if (ordered.Count == 0)
            return Array.Empty<FilledMeterReading>();

        var result = ordered
            .Select(reading => new FilledMeterReading(reading.TimeSeconds, reading.Meter, false))
            .ToList();

        var lastRead = -1;
        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Meter is not { } right)
                continue;

            if (lastRead >= 0 && index - lastRead > 1)
                FillBetween(ordered, result, lastRead, index, right, options);

            lastRead = index;
        }

        return result;
    }

    private static void FillBetween(
        List<MeterReading> ordered,
        List<FilledMeterReading> result,
        int leftIndex,
        int rightIndex,
        double rightMeter,
        MeterGapFillOptions options)
    {
        var leftMeter = ordered[leftIndex].Meter!.Value;
        var leftTime = ordered[leftIndex].TimeSeconds;
        var rightTime = ordered[rightIndex].TimeSeconds;

        var span = rightTime - leftTime;
        if (span <= 0.0 || span > options.MaxGapSeconds)
            return;

        // Richtungswechsel: Die Kamera ist zurueckgefahren, dazwischen laesst sich
        // nichts sinnvoll annehmen.
        if (rightMeter < leftMeter)
            return;

        for (var index = leftIndex + 1; index < rightIndex; index++)
        {
            var anteil = (ordered[index].TimeSeconds - leftTime) / span;
            var wert = leftMeter + ((rightMeter - leftMeter) * anteil);
            result[index] = new FilledMeterReading(ordered[index].TimeSeconds, wert, true);
        }
    }
}
