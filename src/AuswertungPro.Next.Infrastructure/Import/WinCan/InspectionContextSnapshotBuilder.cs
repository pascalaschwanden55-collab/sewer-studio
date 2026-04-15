using System.Text;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Baut InspectionContextSnapshot fuer Echtzeit-Prompt-Injection.
/// Wird im Codier-Modus pro Frame aufgerufen.
/// </summary>
public static class InspectionContextSnapshotBuilder
{
    /// <summary>
    /// Erstellt einen InspectionContextSnapshot fuer die aktuelle Kamera-Position.
    /// </summary>
    /// <param name="haltungKey">Eindeutiger Bezeichner der Haltung</param>
    /// <param name="haltungLaengeM">Gesamtlaenge der Haltung in Metern (kann null sein)</param>
    /// <param name="currentMeter">Aktuelle Kamera-Position in Metern</param>
    /// <param name="bisherigeCodes">Liste aller bisher codierten Codes in dieser Haltung</param>
    /// <param name="patterns">Aggregierte Muster aus der gesamten Datenbank</param>
    public static InspectionContextSnapshot Build(
        string haltungKey,
        double? haltungLaengeM,
        double currentMeter,
        List<string> bisherigeCodes,
        AggregatedPatterns patterns)
    {
        // 1. Relative Position (0.0-1.0, 0 wenn Laenge unbekannt)
        var relativePosition = haltungLaengeM.HasValue && haltungLaengeM.Value > 0
            ? Math.Clamp(currentMeter / haltungLaengeM.Value, 0.0, 1.0)
            : 0.0;

        // 2. Geschaetzter Ansichtstyp basierend auf Position und bisherigen Codes
        string estimatedViewType;
        if (currentMeter <= 0)
            estimatedViewType = "schacht";
        else if (bisherigeCodes.Count == 0)
            estimatedViewType = "uebergang";
        else
            estimatedViewType = "axial";

        // 3. Distanz seit letztem Code (0 wenn noch kein Code erfasst)
        // Hinweis: bisherigeCodes enthaelt nur Code-Strings, keine Meterangaben.
        // Distanz kann nur berechnet werden wenn Meterangaben mitgegeben werden.
        // Hier vereinfachte Variante: 0 wenn keine Codes vorhanden.
        var distanceSinceLastCode = 0.0;

        // 4. Zeit seit letztem Code: 0 (nicht berechenbar ohne Zeitinfo, wird spaeter live gesetzt)
        var timeSinceLastCode = 0.0;

        // 5. Letzte 5 Codes aus bisherigeCodes
        var lastCodes = bisherigeCodes.Count <= 5
            ? new List<string>(bisherigeCodes)
            : bisherigeCodes.Skip(bisherigeCodes.Count - 5).ToList();

        // 6. Erwartete naechste Codes aus Uebergangsmatrix berechnen
        var expectedNextCodes = BerechneErwarteteNaechsteCodes(lastCodes, patterns);

        // 7. Geschwindigkeitsschaetzung aus aggregierten Mustern
        var speedEstimate = patterns.MedianFahrgeschwindigkeit;

        // 8. Detail-Inspektion: Standard false, wird spaeter live gesetzt
        var isLikelyDetailInspection = false;

        return new InspectionContextSnapshot(
            HaltungKey: haltungKey,
            HaltungLaengeM: haltungLaengeM,
            CurrentMeter: currentMeter,
            RelativePosition: relativePosition,
            EstimatedViewType: estimatedViewType,
            DistanceSinceLastCode: distanceSinceLastCode,
            TimeSinceLastCodeSec: timeSinceLastCode,
            LastCodes: lastCodes,
            ExpectedNextCodes: expectedNextCodes,
            SpeedEstimateMps: speedEstimate,
            IsLikelyDetailInspection: isLikelyDetailInspection);
    }

    /// <summary>
    /// Erzeugt einen lesbaren Prompt-Block aus dem Snapshot.
    /// Wird als Kontext-Injection vor dem Bild-Prompt eingefuegt.
    /// </summary>
    public static string ToPromptText(InspectionContextSnapshot snap)
    {
        var sb = new StringBuilder();

        sb.AppendLine("INSPEKTIONS-KONTEXT:");

        // Position in der Haltung
        var laengeText = snap.HaltungLaengeM.HasValue
            ? $"{snap.HaltungLaengeM.Value:F1}m"
            : "unbekannt";
        sb.AppendLine($"Position: {snap.CurrentMeter:F1}m von {laengeText} ({snap.RelativePosition * 100:F0}% der Haltung)");

        // Letzte Codes (kommasepariert)
        var letzteCodesText = snap.LastCodes.Count > 0
            ? string.Join(", ", snap.LastCodes)
            : "(noch keine)";
        sb.AppendLine($"Letzte Codes: {letzteCodesText}");

        // Distanz seit letztem Code
        sb.AppendLine($"Distanz seit letztem Code: {snap.DistanceSinceLastCode:F1}m");

        // Erwartete naechste Codes mit Wahrscheinlichkeit
        if (snap.ExpectedNextCodes.Count > 0)
        {
            var naechsteText = string.Join(", ",
                snap.ExpectedNextCodes.Select(p => $"{p.CodePattern} ({p.Probability * 100:F0}%)"));
            sb.AppendLine($"Erwartete naechste Codes: {naechsteText}");
        }
        else
        {
            sb.AppendLine("Erwartete naechste Codes: (keine Vorhersage)");
        }

        // Fahrgeschwindigkeit
        sb.AppendLine($"Geschwindigkeit: ~{snap.SpeedEstimateMps:F2} m/s");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Berechnet die wahrscheinlichsten naechsten Codes aus der Uebergangsmatrix.
    /// Nimmt den letzten bekannten Code und sucht alle Transitionen die mit ihm beginnen.
    /// Gibt Top 3 nach Haeufigkeit, normalisiert auf 0.0-1.0.
    /// </summary>
    private static List<CodePrediction> BerechneErwarteteNaechsteCodes(
        List<string> lastCodes,
        AggregatedPatterns patterns)
    {
        if (lastCodes.Count == 0 || patterns.UebergangsMatrix.Count == 0)
            return [];

        // Letzten Code als Ausgangspunkt nehmen
        var letzterCode = lastCodes[^1];

        // Alle Eintraege in der Matrix finden die mit "letzterCode→" beginnen
        // Format der Matrix-Keys: z.B. "BCD→BCA" oder "BCA→BAB"
        var trennzeichen = new[] { "→", "->", "|" };

        var passende = new List<(string ZielCode, int Haeufigkeit)>();

        foreach (var (key, haeufigkeit) in patterns.UebergangsMatrix)
        {
            // Trennzeichen im Key suchen
            string? quellCode = null;
            string? zielCode = null;

            foreach (var trenner in trennzeichen)
            {
                var idx = key.IndexOf(trenner, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    quellCode = key[..idx].Trim();
                    zielCode = key[(idx + trenner.Length)..].Trim();
                    break;
                }
            }

            if (quellCode == null || zielCode == null) continue;

            // Pruefe ob der Quell-Code zum letzten Code passt
            if (string.Equals(quellCode, letzterCode, StringComparison.OrdinalIgnoreCase))
            {
                passende.Add((zielCode, haeufigkeit));
            }
        }

        if (passende.Count == 0) return [];

        // Nach Haeufigkeit sortieren (absteigend), Top 3 nehmen
        var sortiert = passende
            .OrderByDescending(p => p.Haeufigkeit)
            .Take(3)
            .ToList();

        // Gesamtanzahl fuer Normalisierung
        var gesamt = sortiert.Sum(p => p.Haeufigkeit);
        if (gesamt == 0) return [];

        return sortiert
            .Select(p => new CodePrediction(
                CodePattern: p.ZielCode,
                Probability: Math.Round((double)p.Haeufigkeit / gesamt, 3)))
            .ToList();
    }
}
