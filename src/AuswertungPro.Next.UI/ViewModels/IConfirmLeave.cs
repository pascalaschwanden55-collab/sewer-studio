namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>
/// Seiten mit ungespeichertem Zustand koennen den Seitenwechsel (oder das
/// Schliessen) stoppen, statt ihre Aenderungen stillschweigend zu verlieren
/// (Audit 2026-06-12, W2: Seitenwechsel verwarf dirty Detail-Edits der
/// Sanierungs-Matrix kommentarlos).
/// </summary>
public interface IConfirmLeave
{
    /// <summary>true = verlassen erlaubt; false = auf der Seite bleiben.</summary>
    bool ConfirmLeave();
}
