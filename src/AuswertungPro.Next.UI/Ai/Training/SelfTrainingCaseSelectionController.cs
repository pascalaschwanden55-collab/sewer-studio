using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingCaseSelectionResult(
    bool ShouldStop,
    TrainingCase? Case,
    string? StatusText);

public static class SelfTrainingCaseSelectionController
{
    public static SelfTrainingCaseSelectionResult Select(
        TrainingCase? selectedCase,
        IEnumerable<TrainingCase> cases,
        IEnumerable<TrainingSample> existingSamples)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(existingSamples);

        if (selectedCase is not null)
            return WithProtocolOrStop(selectedCase);

        var processedIds = existingSamples
            .Select(s => s.CaseId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var firstUnprocessed = cases.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.ProtocolPath) && !processedIds.Contains(c.CaseId));

        if (firstUnprocessed is not null)
            return new SelfTrainingCaseSelectionResult(false, firstUnprocessed, null);

        var withProtocol = cases.Count(c => !string.IsNullOrEmpty(c.ProtocolPath));
        var status = withProtocol > 0
            ? $"Alle {withProtocol} Faelle bereits verarbeitet. Waehle manuell fuer erneutes Training."
            : "Keine Faelle mit Protokoll vorhanden. Bitte zuerst Ordner waehlen und scannen.";

        return new SelfTrainingCaseSelectionResult(true, null, status);
    }

    private static SelfTrainingCaseSelectionResult WithProtocolOrStop(TrainingCase selectedCase)
    {
        if (!string.IsNullOrEmpty(selectedCase.ProtocolPath))
            return new SelfTrainingCaseSelectionResult(false, selectedCase, null);

        return new SelfTrainingCaseSelectionResult(
            true,
            selectedCase,
            "Der ausgewaehlte Fall hat kein Protokoll (PDF).");
    }
}
