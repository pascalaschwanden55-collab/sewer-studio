using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Stellt sicher, dass Haltungslaenge_m gesetzt ist.
    /// Fallback-Kette: Haltungslaenge_m -> Laenge_m -> DamageOverlay -> Protokoll BCE -> manuelle Eingabe.
    /// </summary>
    private void EnsureHaltungslaenge(HaltungRecord record)
    {
        CodingHaltungslaengeEnsureWorkflow.Ensure(
            record,
            _playbackContext.DamageOverlay?.PipeLengthMeters);
    }
}
