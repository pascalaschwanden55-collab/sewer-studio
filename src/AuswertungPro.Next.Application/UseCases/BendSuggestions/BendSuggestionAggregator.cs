using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.BendSuggestions;

/// <summary>Ein Treffer des Bogen-Kandidaten auf genau einem Videobild.</summary>
/// <param name="TimeSeconds">Videozeit des Bildes.</param>
/// <param name="Meter">Meterstand, falls vorhanden. Null = unbekannt.</param>
/// <param name="Confidence">Konfidenz des staerksten Treffers im Bild.</param>
/// <param name="MeterIsEstimated">
/// True, wenn der Meterstand nicht aus dem OSD gelesen, sondern aus der Zeit
/// geschaetzt wurde (siehe VideoFullAnalysisService.EstimateMeter). Ein
/// geschaetzter Wert waechst immer monoton und taugt deshalb nicht, um dieselbe
/// Stelle bei einer erneuten Kamerafahrt wiederzuerkennen.
/// </param>
public sealed record BendFrameDetection(
    double TimeSeconds,
    double? Meter,
    double Confidence,
    bool MeterIsEstimated = false);

/// <summary>Wie sicher ein Vorschlag ist. Die Grenze stammt aus der Videomessung.</summary>
public enum BendSuggestionStrength
{
    Weak = 0,
    Strong = 1
}

/// <summary>Ein zusammengefasster Vorschlag an einer Stelle der Haltung.</summary>
/// <param name="MeterStart">Null, wenn zu keinem Bild ein Meterstand vorlag.</param>
/// <param name="MeterIsEstimated">
/// True, wenn die Meterangabe geschaetzt ist. Sie bleibt als grobe Lage brauchbar,
/// darf aber nicht als gemessene Position dargestellt werden.
/// </param>
public sealed record BendSuggestion(
    double? MeterStart,
    double? MeterEnd,
    double PeakTimeSeconds,
    double MaxConfidence,
    int FrameCount,
    BendSuggestionStrength Strength,
    bool MeterIsEstimated = false);

/// <summary>
/// Regeln der Zusammenfassung. Die Vorgaben sind gemessen, nicht geschaetzt:
/// Arbeitspunkt und Meterabstand stammen aus der Videomessung vom 2026-08-07
/// und der menschlichen Blindpruefung aller 64 Meldungen.
/// </summary>
public sealed record BendSuggestionOptions
{
    /// <summary>Arbeitspunkt: halbe Fehlalarmlast bei gleichem Recall wie 0,25.</summary>
    public double MinConfidence { get; init; } = 0.50;

    /// <summary>Oberhalb dieser Grenze gab es in der Messung keinen Fehlalarm.</summary>
    public double StrongConfidence { get; init; } = 0.70;

    /// <summary>Wie der produktive TemporalFindingDeduplicator: 1,0 m.</summary>
    public double MeterMergeGapMaxMeters { get; init; } = 1.0;

    /// <summary>Rueckfall, solange kein Meterstand lesbar ist.</summary>
    public double TimeMergeGapMaxSeconds { get; init; } = 3.0;

    /// <summary>Schachteinfahrt: der Blick ins Rohr sieht aus wie ein Bogen.</summary>
    public double MinMeter { get; init; } = 0.2;

    /// <summary>Schacht-Trimmung ohne Meterstand.</summary>
    public double SkipFirstSeconds { get; init; } = 3.0;
}

/// <summary>
/// Fasst Einzelbild-Treffer zu Vorschlaegen zusammen — meterbasiert, nicht
/// zeitbasiert. Der Unterschied ist gemessen: Die Kamera durchfaehrt eine Stelle
/// mehrfach (erkennen, zuruecksetzen, nochmals anfahren). Ueber die Zeit gerechnet
/// entstehen daraus mehrere Meldungen; das hat die Fehlalarmlast von 1,0 auf 2,8
/// je Haltung aufgeblaeht. Ueber den Meterstand bleibt es eine Stelle.
///
/// Reine Rechenlogik ohne Datei-, Modell- oder Oberflaechenbezug.
/// </summary>
public static class BendSuggestionAggregator
{
    public static IReadOnlyList<BendSuggestion> Aggregate(
        IEnumerable<BendFrameDetection>? detections,
        BendSuggestionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (detections is null)
            return Array.Empty<BendSuggestion>();

        var relevant = detections
            .Where(detection => detection is not null)
            .Where(detection => detection.Confidence >= options.MinConfidence)
            .Where(detection => !IsShaftEntry(detection, options))
            .OrderBy(detection => detection.TimeSeconds)
            .ToList();
        if (relevant.Count == 0)
            return Array.Empty<BendSuggestion>();

        var groups = new List<Group>();
        foreach (var detection in relevant)
        {
            var target = FindGroup(groups, detection, options);
            if (target is null)
            {
                groups.Add(new Group(detection));
                continue;
            }

            target.Add(detection);
        }

        // Nach Meter geordnet, wie der Mensch die Haltung abfaehrt. Vorschlaege ohne
        // Ortsangabe sind am wenigsten verwertbar und stehen deshalb am Ende.
        return groups
            .Select(group => group.ToSuggestion(options))
            .OrderBy(suggestion => suggestion.MeterStart ?? double.MaxValue)
            .ThenBy(suggestion => suggestion.PeakTimeSeconds)
            .ToList();
    }

    /// <summary>Nur ein gelesener Meterstand ist als Ort belastbar.</summary>
    private static double? ReliableMeter(BendFrameDetection detection)
        => detection.MeterIsEstimated ? null : detection.Meter;

    private static bool IsShaftEntry(BendFrameDetection detection, BendSuggestionOptions options)
        => ReliableMeter(detection) is { } meter
            ? meter < options.MinMeter
            : detection.TimeSeconds < options.SkipFirstSeconds;

    /// <summary>
    /// Sucht die passende Stelle. Mit gelesenem Meterstand entscheidet ausschliesslich
    /// der Abstand zum bereits beobachteten Meterbereich — dieselbe Regel wie im
    /// produktiven TemporalFindingDeduplicator. Zeitlich weit auseinander liegende
    /// Durchfahrten derselben Stelle gehoeren dadurch zusammen. Ist der Meterstand
    /// geschaetzt oder unbekannt, bleibt nur die Zeit.
    /// </summary>
    private static Group? FindGroup(
        List<Group> groups,
        BendFrameDetection detection,
        BendSuggestionOptions options)
    {
        if (ReliableMeter(detection) is { } meter)
        {
            return groups
                .Where(group => group.HasMeter)
                .Select(group => (group, distance: group.DistanceTo(meter)))
                .Where(pair => pair.distance <= options.MeterMergeGapMaxMeters)
                .OrderBy(pair => pair.distance)
                .Select(pair => pair.group)
                .FirstOrDefault();
        }

        // Ohne Meterstand bleibt nur die Zeit. Nur die zuletzt gefuellte Gruppe
        // ohne Meter kommt in Frage — sonst wuerden entfernte Stellen verschmelzen.
        var last = groups.LastOrDefault(group => !group.HasMeter);
        return last is not null
            && detection.TimeSeconds - last.LastTimeSeconds <= options.TimeMergeGapMaxSeconds
                ? last
                : null;
    }

    private sealed class Group
    {
        private double? _meterMin;
        private double? _meterMax;
        private bool _anyMeterEstimated;

        internal Group(BendFrameDetection first)
        {
            HasMeter = ReliableMeter(first).HasValue;
            LastTimeSeconds = first.TimeSeconds;
            PeakTimeSeconds = first.TimeSeconds;
            MaxConfidence = first.Confidence;
            FrameCount = 1;
            TakeMeter(first);
        }

        /// <summary>True, wenn diese Stelle ueber gelesene Meterstaende gebildet wurde.</summary>
        internal bool HasMeter { get; }

        internal double LastTimeSeconds { get; private set; }
        internal double PeakTimeSeconds { get; private set; }
        internal double MaxConfidence { get; private set; }
        internal int FrameCount { get; private set; }

        internal double DistanceTo(double meter)
        {
            if (_meterMin is not { } min || _meterMax is not { } max)
                return double.MaxValue;
            if (meter < min)
                return min - meter;
            return meter > max ? meter - max : 0.0;
        }

        internal void Add(BendFrameDetection detection)
        {
            TakeMeter(detection);
            LastTimeSeconds = Math.Max(LastTimeSeconds, detection.TimeSeconds);
            if (detection.Confidence > MaxConfidence)
            {
                MaxConfidence = detection.Confidence;
                PeakTimeSeconds = detection.TimeSeconds;
            }

            FrameCount++;
        }

        /// <summary>
        /// Ein geschaetzter Meterstand taugt nicht zum Zusammenfassen, bleibt aber als
        /// grobe Lage erhalten — der Mensch soll wissen, wo ungefaehr zu schauen ist.
        /// </summary>
        private void TakeMeter(BendFrameDetection detection)
        {
            if (detection.Meter is not { } meter)
                return;

            _meterMin = _meterMin is { } min ? Math.Min(min, meter) : meter;
            _meterMax = _meterMax is { } max ? Math.Max(max, meter) : meter;
            if (detection.MeterIsEstimated)
                _anyMeterEstimated = true;
        }

        internal BendSuggestion ToSuggestion(BendSuggestionOptions options) => new(
            _meterMin,
            _meterMax,
            PeakTimeSeconds,
            MaxConfidence,
            FrameCount,
            MaxConfidence >= options.StrongConfidence
                ? BendSuggestionStrength.Strong
                : BendSuggestionStrength.Weak,
            _anyMeterEstimated);
    }
}
