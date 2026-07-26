namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Folgefehler-Zaehler fuer Qwen/Ollama. Qwen laeuft in einem eigenen Prozess
/// und gehoert bewusst NICHT in den Sidecar-Ausfallschutz: Ab der Schwelle
/// wird der Lauf nur einmalig als degraded notiert, nie abgebrochen.
/// </summary>
internal sealed class QwenOutageTracker
{
    private readonly int _limit;

    public QwenOutageTracker(int limit) => _limit = limit;

    public int ConsecutiveErrors { get; private set; }

    /// <summary>true, sobald die Schwelle einmal erreicht wurde (Notiz faellig).</summary>
    public bool Noted { get; private set; }

    /// <summary>
    /// Folgefehler-Zahl zum Zeitpunkt der Notiz. Bleibt fuer die Endmeldung erhalten,
    /// auch wenn ein spaeterer Erfolg die laufende Serie auf 0 zuruecksetzt.
    /// </summary>
    public int NotedErrorCount { get; private set; }

    public void RegisterSuccess() => ConsecutiveErrors = 0;

    /// <summary>true genau beim erstmaligen Erreichen der Schwelle (fuer Log-Zeile).</summary>
    public bool RegisterFailure()
    {
        if (Noted)
            return false;
        if (++ConsecutiveErrors < _limit)
            return false;
        Noted = true;
        NotedErrorCount = ConsecutiveErrors;
        return true;
    }
}
