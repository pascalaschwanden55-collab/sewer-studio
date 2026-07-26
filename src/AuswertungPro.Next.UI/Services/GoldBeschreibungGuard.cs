using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Erkennt unbearbeitete Beschreibungs-Vorlagen ("... — Ausmass ergaenzen"): ein Goldsample darf
/// nicht mit Platzhalter-Text gespeichert werden, weil die Beschreibung direkt in KB-Retrieval
/// und Teacher-Labels fliesst.
/// </summary>
public static class GoldBeschreibungGuard
{
    /// <summary>True, wenn der Text noch die automatische Platzhalter-Formulierung enthaelt.</summary>
    public static bool IsPlaceholder(string? beschreibung)
        => GoldDescriptionPolicy.IsPlaceholder(beschreibung);
}
