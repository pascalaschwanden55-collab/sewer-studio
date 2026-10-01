using AuswertungPro.Next.Application.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

/// <summary>
/// Prueft die Gold-Ausrichtungen eines Proto-Negativ-Sets und liefert je physischer Haltung den erzwungenen
/// Split. Aus <see cref="TrainingExportRegistryFileStore"/> ausgelagert, damit die eingefrorene Klasse nicht waechst.
/// </summary>
internal static class NegativeSetGoldAusrichtung
{
    internal static Dictionary<string, TrainingExportTarget> ErzwungeneSplits(
        IEnumerable<string> bekannteHaltungen,
        IReadOnlyList<NegativeSetGoldAlignmentFileDocument> ausrichtungen)
    {
        var knownHoldings = bekannteHaltungen.ToHashSet(StringComparer.Ordinal);
        var forcedSplits = new Dictionary<string, TrainingExportTarget>(StringComparer.Ordinal);
        foreach (var alignment in ausrichtungen)
        {
            if (alignment is null
                || string.IsNullOrWhiteSpace(alignment.PhysicalHoldingKey)
                || !knownHoldings.Contains(alignment.PhysicalHoldingKey)
                || alignment.GoldRole is not ("train" or "val" or "test"))
            {
                throw new TrainingExportPlanException(
                    "Eine Gold-Ausrichtung im Negativ-Set-Manifest ist ungültig.");
            }
            // Entscheid 30.09.2026: Gold-Testhaltungen stehen in keinem Split, auch nicht
            // in validation (steuert Early Stopping). Der Store kennt den Gold-Split nicht;
            // er sieht nur die Rolle, die das Manifest selbst in der Ausrichtung nennt.
            if (alignment.GoldRole == "test")
            {
                throw new TrainingExportPlanException(
                    $"Das Negativ-Set enthält ein Bild aus der eingefrorenen Gold-Testhaltung '{alignment.PhysicalHoldingKey}'. "
                    + "Gold-Testhaltungen dürfen in keinem Split stehen, auch nicht in validation.");
            }
            var expectedForcedSplit = alignment.GoldRole == "train" ? "train" : "validation";
            if (!string.Equals(alignment.ForcedSplit, expectedForcedSplit, StringComparison.Ordinal)
                || !forcedSplits.TryAdd(
                    alignment.PhysicalHoldingKey,
                    alignment.GoldRole == "train"
                        ? TrainingExportTarget.Train
                        : TrainingExportTarget.Validation))
            {
                throw new TrainingExportPlanException(
                    "Eine Gold-Ausrichtung im Negativ-Set-Manifest ist ungültig.");
            }
        }
        return forcedSplits;
    }
}
