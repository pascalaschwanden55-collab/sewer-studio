namespace AuswertungPro.Next.UI.Services;

/// <summary>Fuehrt rechen- und dateiintensive Exportarbeit ausserhalb des UI-Threads aus.</summary>
internal static class BackgroundFileExportRunner
{
    public static Task RunAsync(Action exportAction)
    {
        ArgumentNullException.ThrowIfNull(exportAction);
        return Task.Run(exportAction);
    }
}
