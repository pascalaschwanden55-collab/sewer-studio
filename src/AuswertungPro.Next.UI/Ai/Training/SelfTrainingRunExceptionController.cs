namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingRunExceptionController
{
    public static void ApplyCanceled(
        Action<string> log,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatus);

        const string message = "Selbsttraining abgebrochen.";
        log(message);
        setStatus(message);
    }

    public static void ApplyFailure(
        Exception exception,
        Action<string> log,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatus);

        log($"FEHLER: {exception.GetType().Name}: {exception.Message}");
        setStatus($"Fehler: {exception.Message}");
    }
}
