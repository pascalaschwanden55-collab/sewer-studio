namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

/// <summary>
/// Legt fest, ob ein Inventarlauf technisch erfolgreich war.
/// Fachliche Warnungen bleiben im Bericht, fehlerhafte aktuelle Quellen nicht.
/// </summary>
public static class TrainingInventoryExitPolicy
{
    private static readonly TrainingInventoryDataKind[] RequiredCurrentSources =
    [
        TrainingInventoryDataKind.TeacherAnnotations,
        TrainingInventoryDataKind.TrainingSamples
    ];

    public static bool IsSuccessful(TrainingDataInventoryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        try
        {
            TrainingInventoryReportValidator.Validate(report);
        }
        catch (InvalidDataException)
        {
            return false;
        }

        if (report.Issues.Any(issue => issue.Severity == TrainingInventoryIssueSeverity.Error))
            return false;

        return RequiredCurrentSources.All(dataKind =>
        {
            var currentSources = report.Sources
                .Where(source => source.DataKind == dataKind
                                 && source.Role == TrainingInventorySourceRole.Current)
                .ToArray();
            return currentSources.Length == 1
                   && currentSources[0].ParseState == TrainingInventoryParseState.Parsed
                   && currentSources[0].ValidationLevel == TrainingInventoryValidationLevel.TypedRecords;
        });
    }
}
