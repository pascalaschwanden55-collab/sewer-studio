namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Die vier Anzeigezustaende eines datengetriebenen Bereichs. Ein ViewModel steuert damit,
/// was der Nutzer sieht — statt Lade-, Leer- und Fehleranzeige auf jeder Seite neu zu bauen.
/// </summary>
public enum StatusHostState
{
    /// <summary>Der eigentliche Inhalt (Tabelle, Liste, …) wird gezeigt.</summary>
    Content,

    /// <summary>Daten werden gerade geladen.</summary>
    Loading,

    /// <summary>Kein Fehler, aber auch keine Daten vorhanden (Leerzustand).</summary>
    Empty,

    /// <summary>Das Laden ist fehlgeschlagen.</summary>
    Error
}
