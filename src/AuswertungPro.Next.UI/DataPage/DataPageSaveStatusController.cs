namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageSaveStatusController
{
    public static void Show(
        string? text,
        Action<string> setStatus,
        Action<bool> setVisible,
        Action stopTimer,
        Action startTimer)
    {
        ArgumentNullException.ThrowIfNull(setStatus);
        ArgumentNullException.ThrowIfNull(setVisible);
        ArgumentNullException.ThrowIfNull(stopTimer);
        ArgumentNullException.ThrowIfNull(startTimer);

        setStatus(string.IsNullOrWhiteSpace(text) ? "Gespeichert" : text);
        setVisible(true);
        stopTimer();
        startTimer();
    }

    public static void Hide(
        Action stopTimer,
        Action<bool> setVisible)
    {
        ArgumentNullException.ThrowIfNull(stopTimer);
        ArgumentNullException.ThrowIfNull(setVisible);

        stopTimer();
        setVisible(false);
    }
}
