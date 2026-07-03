namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Status-Badge der Projektuebersicht: entscheidet anhand des echten
/// Persistenz-Zustands (Datei auf Platte, ungespeicherte Aenderungen),
/// nicht anhand des Navigations-Zustands der Shell. Vorher zeigte der Badge
/// nach "Projekt wechseln" faelschlich "noch nicht gespeichert" fuer ein
/// komplett gespeichertes Projekt.
/// </summary>
public static class OverviewProjectStatusPolicy
{
    public static string Build(bool isDirty, bool hasPersistedProject)
        => isDirty ? "Ungespeicherte Aenderungen"
           : hasPersistedProject ? "Projekt gespeichert"
           : "Projekt noch nicht gespeichert";
}
