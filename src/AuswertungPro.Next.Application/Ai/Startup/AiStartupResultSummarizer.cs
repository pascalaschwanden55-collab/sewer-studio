namespace AuswertungPro.Next.Application.Ai.Startup;

// -----------------------------------------------------------------------
// Leitet aus einem AiStartupResult einen kurzen Statustext ab, der im
// Statusbalken der App ("KI BEREIT" / "KI gestartet") angezeigt wird.
// Reine Logik, kein Zustand, kein UI.
// -----------------------------------------------------------------------

public static class AiStartupResultSummarizer
{
    /// <summary>
    /// Gibt einen kurzen, menschenlesbaren Statustext zurueck,
    /// der den Abschluss des KI-Startvorgangs beschreibt.
    /// </summary>
    public static string BuildRuntimeStatusText(AiStartupResult result)
    {
        if (result.HasWarnings)
            return "KI gestartet mit Warnung";

        if (result.PreloadedModels.Count > 0)
            return "Modelle geladen";

        return result.OllamaStartAttempted || result.SidecarStartAttempted
            ? "KI gestartet"
            : "KI bereit";
    }
}
