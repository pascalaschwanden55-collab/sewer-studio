namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseCheckStartResult(
    bool ShouldStop,
    bool IsBusy,
    string? StatusText);

public static class TrainingKnowledgeBaseCheckRunController
{
    public static TrainingKnowledgeBaseCheckStartResult TryStart(bool isBusy)
    {
        if (isBusy)
            return new TrainingKnowledgeBaseCheckStartResult(true, false, null);

        return new TrainingKnowledgeBaseCheckStartResult(
            ShouldStop: false,
            IsBusy: true,
            StatusText: "Prüfe Knowledge Base...");
    }

    public static void ApplySuccess(
        TrainingKnowledgeBaseCheckPresentation presentation,
        Action<string> log,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatus);

        foreach (var line in presentation.LogLines)
            log(line);

        setStatus(presentation.StatusText);
    }

    public static void ApplyFailure(
        Exception exception,
        Action<string> log,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatus);

        setStatus($"KB-Prüfung fehlgeschlagen: {exception.Message}");
        log($"KB-Prüfung FEHLER: {exception.Message}");
    }
}
