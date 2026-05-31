namespace AuswertungPro.Next.UI.LiveControl;

/// <summary>
/// Schmale Bruecke zwischen Live-Control-Server (existiert ab Programmstart) und
/// der DataPage (entsteht erst beim Navigieren). Die DataPage registriert ihren
/// Retry-Handler; der Server ruft ihn ueber <see cref="Invoke"/> auf.
/// Haelt selbst KEINE Projektdaten und keine Pipeline – nur den Verweis auf den Handler.
/// </summary>
public static class LiveControlRetryBridge
{
    private static readonly object Gate = new();
    private static Func<string, LiveControlRetryResult>? _handler;

    /// <summary>Registriert den Retry-Handler der DataPage (letzte Registrierung gewinnt).</summary>
    public static void Register(Func<string, LiveControlRetryResult> handler)
    {
        lock (Gate)
            _handler = handler;
    }

    /// <summary>Entfernt den registrierten Handler (z.B. fuer Tests oder beim Schliessen).</summary>
    public static void Reset()
    {
        lock (Gate)
            _handler = null;
    }

    /// <summary>
    /// Startet die Wiederholung der KI-Videoanalyse fuer eine Haltung.
    /// Liefert eine sofortige Rueckmeldung (gefunden/gestartet bzw. Fehlertext) –
    /// die eigentliche Analyse laeuft im App-Fenster weiter.
    /// </summary>
    public static LiveControlRetryResult Invoke(string haltungsname)
    {
        if (string.IsNullOrWhiteSpace(haltungsname))
            return new LiveControlRetryResult(false, "Haltungsname fehlt.");

        Func<string, LiveControlRetryResult>? handler;
        lock (Gate)
            handler = _handler;

        return handler is null
            ? new LiveControlRetryResult(false, "Datenseite nicht geoeffnet – bitte ein Projekt mit Haltungen laden.")
            : handler(haltungsname.Trim());
    }
}

/// <summary>Ergebnis eines Retry-Aufrufs: ob er angenommen wurde und ein Klartext-Hinweis.</summary>
public sealed record LiveControlRetryResult(bool Ok, string Message);
