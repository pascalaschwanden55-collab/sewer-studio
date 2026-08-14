using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.QualityGate;

/// <summary>
/// Ordnet jedes Evidenzsignal seiner BELEGQUELLE zu (Gesamtaudit 2026-08-14, P1-4).
///
/// Vorher verlangte "Gruen" zwei vorhandene Signalwerte. Das war zu schwach, weil
/// mehrere Signale aus derselben Quelle stammen:
///
/// * <c>LlmCodeConf</c> ist die Sicherheit der Code-Zuordnung des Sprachmodells.
/// * <c>PlausibilityScore</c> wird im Protokollweg aus genau derselben Pruefung
///   uebernommen — er ist kein zweiter Beleg, sondern derselbe Wert unter anderem Namen.
/// * <c>QwenVisionConf</c> ist die Bildbeschreibung desselben Modells, auf der die
///   Code-Zuordnung aufbaut.
/// * <c>KbSimilarity</c> ist die Aehnlichkeit der Beispiele, die dem Sprachmodell im
///   Prompt mitgegeben wurden. Sie hat das Modell beeinflusst und kann es deshalb
///   nicht bestaetigen.
///
/// Diese vier bilden zusammen EINE Quelle. Wirklich unabhaengig davon sind die
/// Bildmodelle im Sidecar und der blinde Datenbankabgleich
/// (<c>KbCodeAgreement</c>), der ohne Code- und Haltungshinweis sucht.
///
/// Bewusste Grenze: Die Gewichtung im Zahlenwert (Composite) bleibt unveraendert.
/// Geaendert wird nur, was als eigenstaendiger Beleg fuer "Gruen" zaehlt.
/// </summary>
public static class EvidenceSourceGrouping
{
    /// <summary>Wie viele voneinander unabhaengige Quellen "Gruen" mindestens braucht.</summary>
    public const int MinIndependentSourcesForGreen = 2;

    public const string SourceYolo = "Bildmodell YOLO";
    public const string SourceDino = "Bildmodell DINO";
    public const string SourceSam = "Segmentierung SAM";
    public const string SourceLanguageModel = "Sprachmodell (inkl. Plausibilitaet und Prompt-Beispiele)";
    public const string SourceBlindKb = "Blinder Datenbankabgleich";

    private static readonly Dictionary<string, string> SourceBySignal =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(EvidenceVector.YoloConf)] = SourceYolo,
            [nameof(EvidenceVector.DinoConf)] = SourceDino,
            [nameof(EvidenceVector.SamMaskStability)] = SourceSam,

            // Eine Quelle: alle vier haengen am selben Sprachmodell-Lauf.
            [nameof(EvidenceVector.QwenVisionConf)] = SourceLanguageModel,
            [nameof(EvidenceVector.LlmCodeConf)] = SourceLanguageModel,
            [nameof(EvidenceVector.PlausibilityScore)] = SourceLanguageModel,
            [nameof(EvidenceVector.KbSimilarity)] = SourceLanguageModel,

            [nameof(EvidenceVector.KbCodeAgreement)] = SourceBlindKb
        };

    /// <summary>
    /// Quelle eines Signals. Ein unbekanntes Signal erhaelt seinen eigenen Namen als
    /// Quelle: neue Signale werden dadurch zunaechst als eigenstaendig behandelt und
    /// muessen bewusst zugeordnet werden — aber sie verschwinden nicht stillschweigend.
    /// </summary>
    public static string SourceOf(string signalName)
        => SourceBySignal.TryGetValue(signalName, out var quelle) ? quelle : signalName;

    /// <summary>Zaehlt die verschiedenen Belegquellen einer Signalmenge.</summary>
    public static IReadOnlyCollection<string> DistinctSources(IEnumerable<string> signalNames)
    {
        var quellen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in signalNames)
            quellen.Add(SourceOf(name));
        return quellen;
    }
}
