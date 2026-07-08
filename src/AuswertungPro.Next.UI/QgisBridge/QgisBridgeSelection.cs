namespace AuswertungPro.Next.UI.QgisBridge;

/// <summary>
/// Merkt sich die zuletzt gewaehlte Haltung fuer die QGIS-Bridge — unabhaengig davon,
/// auf welcher Seite oder in welchem Fenster (Haltungen-Seite, Karte-Seite, KarteWindow)
/// die Auswahl passiert ist. Eine Abwahl (null/leer) loescht die Auswahl bewusst NICHT:
/// QGIS soll die zuletzt bearbeitete Haltung weiter anzeigen, auch wenn der Nutzer
/// zwischenzeitlich auf eine andere Seite navigiert.
/// Beim Projektwechsel wird die Auswahl zurueckgesetzt.
/// </summary>
internal static class QgisBridgeSelection
{
    private static readonly object Gate = new();
    private static Guid _projectId = Guid.Empty;
    private static string _current = "";
    private static long _stamp;
    private static string _currentSchacht = "";
    private static long _schachtStamp;

    /// <summary>Wird nach jeder (neuen) Auswahl ausgeloest — auch bei erneutem Klick auf dieselbe
    /// Haltung. Die In-App-Karte haengt sich hier ein, um wie QGIS auf die Haltung zu zoomen.</summary>
    public static event Action? SelectionChanged;

    /// <summary>Wie <see cref="SelectionChanged"/>, aber fuer die Schacht-Auswahl (eigener Kanal,
    /// damit Haltungs- und Schacht-Zoom sich nicht gegenseitig ueberschreiben).</summary>
    public static event Action? SchachtSelectionChanged;

    /// <summary>Meldet eine (neue) Auswahl. Leere Werte werden ignoriert (sticky).</summary>
    public static void Set(string? haltungsname)
    {
        var value = haltungsname?.Trim();
        if (string.IsNullOrEmpty(value))
            return;

        lock (Gate)
        {
            _current = value;
            // Jeder Klick zaehlt — auch auf dieselbe Haltung. QGIS zoomt bei jedem
            // neuen Stempel wieder hin, nicht nur beim Haltungswechsel.
            _stamp++;
        }

        // Ausserhalb des Locks benachrichtigen (kein Deadlock; Subscriber laeuft frei).
        SelectionChanged?.Invoke();
    }

    /// <summary>Laufender Zaehler der Auswahl-Klicks (fuer den Auto-Zoom im Plugin).</summary>
    public static long Stamp
    {
        get
        {
            lock (Gate)
                return _stamp;
        }
    }

    /// <summary>Meldet eine (neue) Schacht-Auswahl. Leere Werte werden ignoriert (sticky), analog
    /// zu <see cref="Set"/>. Eigener Stempel, damit QGIS unabhaengig von Haltungen auf den Schacht zoomt.</summary>
    public static void SetSchacht(string? schachtnummer)
    {
        var value = schachtnummer?.Trim();
        if (string.IsNullOrEmpty(value))
            return;

        lock (Gate)
        {
            _currentSchacht = value;
            _schachtStamp++;
        }

        SchachtSelectionChanged?.Invoke();
    }

    /// <summary>Laufender Zaehler der Schacht-Auswahl-Klicks (fuer den Auto-Zoom im Plugin).</summary>
    public static long SchachtStamp
    {
        get
        {
            lock (Gate)
                return _schachtStamp;
        }
    }

    /// <summary>
    /// Liefert die aktuelle Auswahl fuer das angegebene Projekt.
    /// Wechselt die Projekt-Id, wird die gemerkte Auswahl verworfen.
    /// </summary>
    public static string CurrentFor(Guid projectId)
    {
        lock (Gate)
        {
            EnsureProject(projectId);
            return _current;
        }
    }

    /// <summary>Wie <see cref="CurrentFor"/>, aber fuer die gemerkte Schacht-Auswahl.</summary>
    public static string CurrentSchachtFor(Guid projectId)
    {
        lock (Gate)
        {
            EnsureProject(projectId);
            return _currentSchacht;
        }
    }

    // Setzt bei Projektwechsel BEIDE Auswahlen zurueck. Aufruf nur unter Gate.
    private static void EnsureProject(Guid projectId)
    {
        if (_projectId == Guid.Empty)
        {
            _projectId = projectId;
        }
        else if (projectId != _projectId)
        {
            _projectId = projectId;
            _current = "";
            _currentSchacht = "";
        }
    }

    /// <summary>Setzt den Zustand zurueck (fuer Tests).</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            _projectId = Guid.Empty;
            _current = "";
            _currentSchacht = "";
        }
    }
}
