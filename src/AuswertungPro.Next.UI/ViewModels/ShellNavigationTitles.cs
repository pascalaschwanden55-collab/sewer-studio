namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Navigationstitel sind Schluessel (ASCII); die Leiste zeigt den Namen mit Umlauten.</summary>
public static class ShellNavigationTitles
{
    public static string Anzeige(string? title) => title switch
    {
        "Uebersicht" => "Übersicht",
        "Schaechte" => "Schächte",
        // Optikanalyse 28.09.2026, Aufgabe 7: Leiste und Seitentitel angleichen. Der Schluessel
        // (NavItem.Title, Handbuch-Schluessel, Navigation) bleibt "Dossiers"/"VSA"; nur die
        // sichtbare Beschriftung wird an den bestehenden Seitentitel (NovaPageHeader) angeglichen.
        "Dossiers" => "Eigentümerdossiers",
        "VSA" => "VSA-Bewertung",
        null => string.Empty,
        _ => title
    };
}

/// <summary>Reine Textregeln der Kopf- und Fusszeile (Inventar 3.2, 3.4).</summary>
public static class ShellNovaKopfzeile
{
    public static string Brotkrume(string? projekt, string? navTitle)
    {
        var seite = ShellNavigationTitles.Anzeige(navTitle);
        return string.IsNullOrWhiteSpace(projekt) ? seite : $"{projekt} / {seite}";
    }

    public static string Speicherstand(string? projekt, System.DateTime? gespeichertLokal, bool ungespeichert)
    {
        if (string.IsNullOrWhiteSpace(projekt))
            return "Kein Projekt geöffnet";
        if (ungespeichert)
            return $"{projekt} · ungespeichert";
        return gespeichertLokal is { } t ? $"{projekt} · gespeichert {t:HH:mm}" : projekt;
    }
}
