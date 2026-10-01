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
/// <param name="TimeStartSeconds">Beginn der Stelle in der Videozeit (fuer Clip und Einordnung).</param>
/// <param name="TimeEndSeconds">Ende der Stelle in der Videozeit.</param>
public sealed record BendSuggestion(
    double? MeterStart,
    double? MeterEnd,
    double PeakTimeSeconds,
    double MaxConfidence,
    int FrameCount,
    BendSuggestionStrength Strength,
    bool MeterIsEstimated = false,
    double TimeStartSeconds = 0.0,
    double TimeEndSeconds = 0.0);

/// <summary>
/// Regeln der Zusammenfassung.
///
/// Die beiden Konfidenzgrenzen haben BEWUSST keinen Standardwert: Sie gehoeren zum
/// einzelnen Gewicht, nicht zum Verfahren. Gemessen am 2026-08-08 auf denselben
/// Videos, drei Modelle aus identischen Daten und Einstellungen, nur der
/// Zufallsstartwert unterschiedlich — bei conf 0,50 fand Seed 44 sieben von zehn
/// Boegen, Seed 46 ebenfalls sieben, Seed 45 nur zwei. Seine Konfidenzen liegen
/// systematisch tiefer; bei 0,25 verhaelt er sich wie die anderen bei 0,50.
///
/// Ein fest verdrahteter Wert wuerde beim naechsten Modellwechsel still auf ein
/// Drittel der Treffer fallen, ohne dass jemand etwas aendert. Der Arbeitspunkt
/// muss deshalb je Kandidat kalibriert und mitgeliefert werden.
///
/// Die uebrigen Werte sind Eigenschaften des Verfahrens, nicht des Modells, und
/// behalten ihre Vorgabe.
/// </summary>
public sealed record BendSuggestionOptions
{
    /// <summary>
    /// Kalibrierter Arbeitspunkt DIESES Gewichts. Er gilt fuer die Stelle als
    /// ganze, nicht fuer das einzelne Bild — siehe <see cref="FloorConfidence"/>.
    /// </summary>
    public required double MinConfidence { get; init; }

    /// <summary>
    /// Aufnahmegrenze fuer das einzelne Bild. Eine Stelle wird ueber mehrere Bilder
    /// gesehen, und die Konfidenz schwankt dabei; wer vor dem Zusammenfassen am
    /// Arbeitspunkt filtert, zerlegt eine Stelle mit einem Einbruch (0,6 – 0,4 –
    /// 0,7) in zwei Vorschlaege. Gemessen am 2026-08-08: auf zwei Haltungen mit
    /// schlechter Meterlesung stieg die Meldungszahl dadurch statt zu sinken.
    /// Unterhalb dieser Grenze wird gar nicht gesammelt, damit ein Rauschtreffer
    /// eine echte Stelle nicht unnoetig in die Laenge zieht.
    /// </summary>
    public double FloorConfidence { get; init; } = 0.10;

    /// <summary>Grenze, ab der ein Vorschlag als stark gilt — ebenfalls je Gewicht.</summary>
    public required double StrongConfidence { get; init; }

    /// <summary>Wie der produktive TemporalFindingDeduplicator: 1,0 m.</summary>
    public double MeterMergeGapMaxMeters { get; init; } = 1.0;

    /// <summary>Rueckfall, solange kein Meterstand lesbar ist.</summary>
    public double TimeMergeGapMaxSeconds { get; init; } = 3.0;

    /// <summary>Schachteinfahrt: der Blick ins Rohr sieht aus wie ein Bogen.</summary>
    public double MinMeter { get; init; } = 0.2;

    /// <summary>
    /// Groesster moeglicher Meterstand dieser Haltung, sofern bekannt. Ein
    /// gelesener Wert darueber ist eine Fehllesung und wird wie ein fehlender
    /// behandelt — er ordnet noch ueber die Zeit zu, verschiebt aber keinen Ort.
    /// Am 2026-08-08 meldete der OSD-Leser 133,08 m in einer Haltung von keinen
    /// 20 m; die Zahlenform allein reicht als Pruefung nicht. Null = unbekannt,
    /// dann wird nichts verworfen: Wer die Laenge nicht kennt, darf nicht raten.
    /// </summary>
    public double? MaxPlausibleMeter { get; init; }

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

        // Am Boden sammeln, nicht am Arbeitspunkt: Der Arbeitspunkt entscheidet
        // erst ueber die fertige Stelle.
        var floor = Math.Min(options.FloorConfidence, options.MinConfidence);
        var relevant = detections
            .Where(detection => detection is not null)
            .Where(detection => detection.Confidence >= floor)
            .Select(detection => DropImplausibleMeter(detection, options))
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
                groups.Add(new Group(detection, options));
                continue;
            }

            target.Add(detection, options);
        }

        // Nach Meter geordnet, wie der Mensch die Haltung abfaehrt. Vorschlaege ohne
        // Ortsangabe sind am wenigsten verwertbar und stehen deshalb am Ende.
        return groups
            .Where(group => group.MaxConfidence >= options.MinConfidence)
            .Select(group => group.ToSuggestion(options))
            .OrderBy(suggestion => suggestion.MeterStart ?? double.MaxValue)
            .ThenBy(suggestion => suggestion.PeakTimeSeconds)
            .ToList();
    }

    /// <summary>
    /// Verwirft eine erkennbare Fehllesung, bevor sie irgendetwas beeinflusst.
    /// Sie gilt danach als unlesbar: Das Bild ordnet noch ueber die Zeit zu,
    /// verschiebt aber keinen Ort und erscheint nicht in der Meterangabe.
    /// </summary>
    private static BendFrameDetection DropImplausibleMeter(
        BendFrameDetection detection,
        BendSuggestionOptions options)
    {
        if (detection.Meter is not { } meter)
            return detection;

        var implausible = meter < 0.0
            || (options.MaxPlausibleMeter is { } max && meter > max);
        return implausible
            ? detection with { Meter = null, MeterIsEstimated = false }
            : detection;
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
            var located = groups.Where(group => group.HasMeterRange).ToList();
            if (located.Count > 0)
            {
                // Es gibt verortete Stellen: Dann entscheidet ausschliesslich der
                // Meterstand. Ein Sprung ueber den Abstand ist eine andere Stelle,
                // auch wenn er zeitlich unmittelbar folgt.
                return located
                    .Select(group => (group, distance: group.DistanceTo(meter)))
                    .Where(pair => pair.distance <= options.MeterMergeGapMaxMeters)
                    .OrderBy(pair => pair.distance)
                    .Select(pair => pair.group)
                    .FirstOrDefault();
            }

            // Noch keine Stelle hat einen Ort — etwa weil bisher nur schwache
            // Bilder gesammelt wurden. Dann ordnet die Zeit zu, damit dieses Bild
            // die begonnene Stelle verortet statt eine zweite zu eroeffnen.
        }

        // Kein Meterbereich passt — oder der Meterstand ist unbekannt. Dann
        // entscheidet die Zeit, und es kommt JEDE Gruppe in Frage: Der OSD-Leser
        // deckt je nach Anzeigestil nur einen Teil der Bilder ab, und eine Luecke
        // mitten in einer Stelle darf sie nicht in zwei Vorschlaege spalten. Der
        // Meterstand dieses Bildes bleibt dabei ungenutzt — er entscheidet nichts,
        // er ordnet nur zu.
        return groups
            .Select(group => (group, distance: group.TimeDistanceTo(detection.TimeSeconds)))
            .Where(pair => pair.distance <= options.TimeMergeGapMaxSeconds)
            .OrderBy(pair => pair.distance)
            .Select(pair => pair.group)
            .FirstOrDefault();
    }

    private sealed class Group
    {
        private double? _meterMin;
        private double? _meterMax;
        private bool _anyMeterEstimated;

        internal Group(BendFrameDetection first, BendSuggestionOptions options)
        {
            FirstTimeSeconds = first.TimeSeconds;
            LastTimeSeconds = first.TimeSeconds;
            PeakTimeSeconds = first.TimeSeconds;
            MaxConfidence = first.Confidence;
            FrameCount = 1;
            TakeMeter(first, options);
        }

        /// <summary>
        /// True, sobald mindestens ein Bild ab dem Arbeitspunkt einen gelesenen
        /// Meterstand beigesteuert hat. Nur dann hat diese Stelle einen Ort.
        /// </summary>
        internal bool HasMeterRange => _meterMin.HasValue;

        internal double FirstTimeSeconds { get; }

        internal double LastTimeSeconds { get; private set; }

        /// <summary>Abstand zum bereits beobachteten Zeitbereich dieser Stelle.</summary>
        internal double TimeDistanceTo(double timeSeconds)
        {
            if (timeSeconds < FirstTimeSeconds)
                return FirstTimeSeconds - timeSeconds;
            return timeSeconds > LastTimeSeconds ? timeSeconds - LastTimeSeconds : 0.0;
        }
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

        internal void Add(BendFrameDetection detection, BendSuggestionOptions options)
        {
            TakeMeter(detection, options);
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
        ///
        /// Schwache Bilder unterhalb des Arbeitspunkts zaehlen dagegen gar nicht in
        /// den gemeldeten Bereich. Sonst waechst eine Stelle Meter fuer Meter mit:
        /// Beim ersten echten Lauf am 2026-08-08 entstand daraus ein Vorschlag ueber
        /// 0,20 bis 3,40 m — ein Fuenftel der Haltung. Sie duerfen zuordnen, aber
        /// den Ort nicht setzen.
        /// </summary>
        private void TakeMeter(BendFrameDetection detection, BendSuggestionOptions options)
        {
            if (detection.Meter is not { } meter)
                return;
            if (detection.Confidence < options.MinConfidence)
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
            _anyMeterEstimated,
            FirstTimeSeconds,
            LastTimeSeconds);
    }
}
