namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13 (Windows-Integration): zeigt den Fortschritt langer
/// Laeufe (Datensicherung, Ein-Knopf-Import) am Programmsymbol in der Windows-Taskleiste - auch
/// sichtbar, wenn das Fenster minimiert oder von anderen Fenstern verdeckt ist.
///
/// Vertrag statt direktem <see cref="System.Windows.Shell.TaskbarItemInfo"/>-Zugriff, damit
/// ViewModels/Workflows nicht auf ein konkretes Fenster angewiesen sind und sich mit einem Fake
/// testen lassen.
/// </summary>
public interface ITaskbarFortschritt
{
    /// <summary>Bestimmter Fortschritt. <paramref name="anteil"/> wird auf 0..1 begrenzt.</summary>
    void SetzeFortschritt(double anteil);

    /// <summary>Laeuft, aber (noch) ohne bezifferbaren Anteil (z. B. "Groessen werden berechnet").</summary>
    void SetzeUnbestimmt();

    /// <summary>Roter Zustand: der Lauf ist fehlgeschlagen.</summary>
    void Fehler();

    /// <summary>Blendet die Anzeige wieder aus (Lauf erfolgreich beendet oder abgebrochen).</summary>
    void Beenden();
}
