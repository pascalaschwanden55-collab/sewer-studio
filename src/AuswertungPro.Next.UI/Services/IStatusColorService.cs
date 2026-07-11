using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Die EINE Farbquelle fuer Status-Semantik (Ampel, Severity, Konfidenz).
/// Theme-abhaengige Farben spiegeln die Severity1..5-/Success-/Warning-/Danger-Brushes
/// der Theme-Dateien; Overlay-Farben sind bewusst theme-unabhaengig (Zeichnen ueber Video/Foto).
/// theme = null nutzt das aktuell aktive Theme (ThemeManager.CurrentTheme).
/// </summary>
public interface IStatusColorService
{
    /// <summary>Schadensschwere 1..5 (gruen -> rot), an das Theme angepasst. Werte ausserhalb werden geklemmt.</summary>
    Color Severity(int severity, string? theme = null);

    /// <summary>Schadensschwere 1..5 fuer Overlays UEBER Video/Frame (gesaettigt, theme-unabhaengig).</summary>
    Color SeverityOverlay(int severity);

    /// <summary>QualityGate-Ampel (Gruen/Gelb/Rot) in der Theme-Semantik Success/Warning/Danger.</summary>
    Color Ampel(TrafficLight gate, string? theme = null);

    /// <summary>Zentrale KI-Entscheidung; null = neutral (Muted).</summary>
    Color Outcome(AiDecisionOutcome? outcome, string? theme = null);

    /// <summary>Konfidenz 0..1: &gt;=0.85 gruen, &gt;=0.60 gelb, darunter rot.</summary>
    Color Confidence(double confidence, string? theme = null);

    /// <summary>Neutrale Beschriftungsfarbe (Muted je Theme).</summary>
    Color Neutral(string? theme = null);

    // Overlay-Palette (Player/Video/Foto) — identisch zu PlayerStatusColors.
    Color OverlaySuccess { get; }
    Color OverlayWarning { get; }
    Color OverlayError { get; }
    Color OverlayInfo { get; }
    Color OverlayMuted { get; }

    /// <summary>Zustandsklasse "0".."4" aus der Excel-Palette (fachlich fix); null wenn unbekannt.</summary>
    Color? Zustandsklasse(string? klasse);
}
