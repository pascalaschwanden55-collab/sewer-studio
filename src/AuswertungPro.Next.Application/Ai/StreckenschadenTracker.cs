using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Reine, testbare Zustandslogik fuer automatische Streckenschaeden (VSA 2.1.2):
/// Ein Befund, der sich ueber mehr als einen Meter erstreckt, wird mit Anfang (A) und Ende (B)
/// gefuehrt. Da es kein Objekt-Tracking gibt, wird die Identitaet ueber Hauptcode + aehnliche
/// Uhrlage bestimmt (User-Entscheidung 2026-06-16): derselbe Hauptcode bei aehnlicher Uhrlage
/// gilt als Fortsetzung derselben Strecke, unabhaengig vom Meterabstand. So ueberlebt eine
/// Strecke einzelne Erkennungsluecken.
///
/// Schwelle: Beim ersten Erkennen wird ein offener Anfang gemerkt. Erst wenn derselbe Schaden
/// spaeter > 1 m weiter noch erkannt wird, ist die Strecke bestaetigt (A...B). Verschwindet er
/// vorher (&lt;= 1 m), wird er als einzelner Punktbefund gefuehrt.
///
/// Die Klasse trifft nur Entscheidungen (Open/Extend/Confirm/Close) — Persistenz, UI und das
/// Setzen von MeterEnd/IsStreckenschaden bleiben im Aufrufer.
/// </summary>
public sealed class StreckenschadenTracker
{
    /// <summary>Schwelle in Metern, ab der eine offene Strecke als echte Strecke (&gt; 1 m) gilt.</summary>
    public const double StreckeMinLengthMeters = 1.0;

    /// <summary>Toleranz der Uhrlage (in Stunden), innerhalb der zwei Befunde als gleiche Lage gelten.</summary>
    public const double ClockToleranceHours = 2.0;

    /// <summary>
    /// Distanz (in Metern), die der Code nicht mehr erkannt werden darf, bevor eine offene Strecke
    /// geschlossen wird. Ueberlebt einzelne Erkennungsluecken (User-Entscheidung 2026-06-16:
    /// "erst nach Toleranz-Distanz weg").
    /// </summary>
    public const double CloseGapMeters = 1.0;

    /// <summary>Ein erkannter Streckenschaden-Befund in einem Analyse-Tick.</summary>
    public sealed record Observation(string MainCode, double? ClockHour, double Meter);

    /// <summary>Interner Zustand einer offenen Strecke.</summary>
    private sealed class OpenSegment
    {
        public required string MainCode { get; init; }
        public double? ClockHour { get; set; }
        public double StartMeter { get; init; }
        public double LastSeenMeter { get; set; }
        public bool Confirmed { get; set; } // wurde die &gt; 1 m Schwelle erreicht?
    }

    private readonly List<OpenSegment> _open = new();

    /// <summary>Art der Aktion, die der Aufrufer ausfuehren soll.</summary>
    public enum SegmentActionType
    {
        /// <summary>Neuen offenen Anfang (A) anlegen.</summary>
        Open,
        /// <summary>Bestehende offene Strecke verlaengern (letzte Sichtung nachfuehren).</summary>
        Extend,
        /// <summary>Offene Strecke schliessen (Ende B) am angegebenen Meter.</summary>
        Close
    }

    /// <summary>Eine vom Tracker beschlossene Aktion.</summary>
    public sealed record SegmentAction(
        SegmentActionType Type,
        string MainCode,
        double? ClockHour,
        double StartMeter,
        double EndMeter,
        bool IsConfirmedStrecke);

    /// <summary>
    /// Verarbeitet einen Analyse-Tick: die in diesem Frame erkannten Streckenschaden-Befunde
    /// (bereits gefiltert auf codierbare Streckenschaden-Codes). Liefert die auszufuehrenden
    /// Aktionen. Codes, die in diesem Tick NICHT mehr erkannt werden, werden geschlossen.
    /// </summary>
    public IReadOnlyList<SegmentAction> Update(IReadOnlyList<Observation> observations, double currentMeter)
    {
        var actions = new List<SegmentAction>();
        var matchedSegments = new HashSet<OpenSegment>();

        foreach (var obs in observations)
        {
            var seg = FindMatch(obs);
            if (seg == null)
            {
                // Neuer offener Anfang.
                var created = new OpenSegment
                {
                    MainCode = obs.MainCode,
                    ClockHour = obs.ClockHour,
                    StartMeter = obs.Meter,
                    LastSeenMeter = obs.Meter,
                    Confirmed = false
                };
                _open.Add(created);
                matchedSegments.Add(created);
                actions.Add(new SegmentAction(
                    SegmentActionType.Open, created.MainCode, created.ClockHour,
                    created.StartMeter, obs.Meter, IsConfirmedStrecke: false));
            }
            else
            {
                // Fortsetzung: letzte Sichtung nachfuehren, ggf. als echte Strecke bestaetigen.
                seg.LastSeenMeter = Math.Max(seg.LastSeenMeter, obs.Meter);
                if (obs.ClockHour.HasValue) seg.ClockHour = obs.ClockHour;
                bool nowConfirmed = (seg.LastSeenMeter - seg.StartMeter) > StreckeMinLengthMeters;
                bool justConfirmed = nowConfirmed && !seg.Confirmed;
                seg.Confirmed = seg.Confirmed || nowConfirmed;
                matchedSegments.Add(seg);
                actions.Add(new SegmentAction(
                    SegmentActionType.Extend, seg.MainCode, seg.ClockHour,
                    seg.StartMeter, seg.LastSeenMeter, IsConfirmedStrecke: seg.Confirmed) );
                // (justConfirmed wird nicht separat gemeldet — der Aufrufer erkennt es an IsConfirmedStrecke)
                _ = justConfirmed;
            }
        }

        // Offene Strecken, die in diesem Tick NICHT mehr erkannt wurden: NICHT sofort schliessen
        // (Erkennungsluecken ueberleben). Erst schliessen, wenn der Code ueber die Toleranz-Distanz
        // CloseGapMeters hinaus gar nicht mehr auftaucht. Das Ende ist die letzte echte Sichtung.
        foreach (var seg in _open.Where(s => !matchedSegments.Contains(s)).ToList())
        {
            if (currentMeter - seg.LastSeenMeter > CloseGapMeters)
            {
                actions.Add(BuildCloseAction(seg, seg.LastSeenMeter));
                _open.Remove(seg);
            }
            // sonst: offen lassen (Luecke), LastSeenMeter bleibt unveraendert.
        }

        return actions;
    }

    /// <summary>
    /// Schliesst ALLE offenen Strecken am angegebenen Meter (z.B. bei Rohrende BCE oder Abbruch BDC).
    /// VSA-Pflicht: bei BCE/BDC duerfen keine offenen Streckenschaeden zurueckbleiben.
    /// </summary>
    public IReadOnlyList<SegmentAction> CloseAll(double currentMeter)
    {
        var actions = _open
            .Select(seg => BuildCloseAction(seg, Math.Max(seg.LastSeenMeter, currentMeter)))
            .ToList();
        _open.Clear();
        return actions;
    }

    /// <summary>Anzahl aktuell offener Strecken (fuer Diagnose/Tests).</summary>
    public int OpenCount => _open.Count;

    /// <summary>Setzt den Tracker zurueck (beim Start einer neuen Codier-Session). Pflicht,
    /// damit keine offenen Strecken aus der Vorsession ueberleben.</summary>
    public void Reset() => _open.Clear();

    private static SegmentAction BuildCloseAction(OpenSegment seg, double endMeter)
        => new(
            SegmentActionType.Close, seg.MainCode, seg.ClockHour,
            seg.StartMeter, endMeter,
            IsConfirmedStrecke: (endMeter - seg.StartMeter) > StreckeMinLengthMeters);

    private OpenSegment? FindMatch(Observation obs)
    {
        return _open.FirstOrDefault(s =>
            string.Equals(s.MainCode, obs.MainCode, StringComparison.OrdinalIgnoreCase)
            && ClockMatches(s.ClockHour, obs.ClockHour));
    }

    /// <summary>
    /// Uhrlagen gelten als gleich, wenn beide unbekannt sind, eine unbekannt ist, oder ihr
    /// Abstand auf dem Zifferblatt (zyklisch, 12 Stunden) innerhalb der Toleranz liegt.
    /// </summary>
    private static bool ClockMatches(double? a, double? b)
    {
        if (!a.HasValue || !b.HasValue)
            return true;
        double diff = Math.Abs(a.Value - b.Value) % 12.0;
        double cyclic = Math.Min(diff, 12.0 - diff);
        return cyclic <= ClockToleranceHours;
    }
}
