namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingLiveAiTimerSettings
{
    public static TimeSpan AnalysisInterval => TimeSpan.FromSeconds(5);

    public static TimeSpan BlinkInterval => TimeSpan.FromMilliseconds(800);

    public static string FormatAnalysisIntervalText()
        => $"Intervall alle {AnalysisInterval.TotalSeconds:0} Sekunden";
}
