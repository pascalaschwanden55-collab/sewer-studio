using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

/// <summary>
/// Die Regel der Freigabe: "Die staerkste gruppierte Meldung des Modells im
/// GANZEN Video. Kein Zeitfenster." Ein Video hat genau einen Rohranfang und
/// genau ein Rohrende — die Regel folgt dem Arbeitsablauf, nicht den Daten,
/// und hat nichts zu justieren.
///
/// Warum kein Zeitfenster: Beim Rohrende hatten alle 25 verpassten Videos eine
/// Meldung mit Konfidenz ~1,00, nur 8 bis 100 s vor dem Ende und damit ausserhalb
/// des Fensters. Mit Fenster 57,6 % Recall, mit dieser Regel 88,4 %.
///
/// Reine Rechenlogik ohne Datei-, Modell- oder Oberflaechenbezug.
/// </summary>
public static class PipeEndSuggestionRule
{
    public static PipeEndSuggestion? Strongest(
        IEnumerable<PipeEndFrameScore>? scores,
        PipeEndKind kind,
        PipeEndRuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (scores is null)
            return null;

        // Gesammelt wird ab dem Boden, nicht ab der Schwelle: Die Schwelle
        // entscheidet erst ueber die fertige Stelle.
        var floor = Math.Min(options.FloorConfidence, options.Threshold);
        var relevant = scores
            .Where(score => score is not null)
            .Where(score => score.Confidence >= floor && score.TimeSeconds >= options.SkipFirstSeconds)
            .OrderBy(score => score.TimeSeconds)
            .ToList();
        if (relevant.Count == 0)
            return null;

        var groups = new List<Group>();
        foreach (var score in relevant)
        {
            if (groups.Count > 0 && score.TimeSeconds - groups[^1].TimeEnd <= options.TimeGapSeconds)
                groups[^1].Add(score);
            else
                groups.Add(new Group(score));
        }

        // Die staerkste Stelle gewinnt; bei Gleichstand die fruehere (wie das
        // Abnahmeskript, das nur bei echtem ">" wechselt).
        Group? best = null;
        foreach (var group in groups)
        {
            if (group.MaxConfidence < options.Threshold)
                continue;
            if (best is null || group.MaxConfidence > best.MaxConfidence)
                best = group;
        }

        return best is null
            ? null
            : new PipeEndSuggestion(
                kind,
                best.TimeStart,
                best.TimeEnd,
                best.PeakTime,
                best.MaxConfidence,
                best.FrameCount);
    }

    private sealed class Group
    {
        public Group(PipeEndFrameScore first)
        {
            TimeStart = first.TimeSeconds;
            TimeEnd = first.TimeSeconds;
            PeakTime = first.TimeSeconds;
            MaxConfidence = first.Confidence;
            FrameCount = 1;
        }

        public double TimeStart { get; }
        public double TimeEnd { get; private set; }
        public double PeakTime { get; private set; }
        public double MaxConfidence { get; private set; }
        public int FrameCount { get; private set; }

        public void Add(PipeEndFrameScore score)
        {
            TimeEnd = score.TimeSeconds;
            FrameCount++;
            if (score.Confidence > MaxConfidence)
            {
                MaxConfidence = score.Confidence;
                PeakTime = score.TimeSeconds;
            }
        }
    }
}
