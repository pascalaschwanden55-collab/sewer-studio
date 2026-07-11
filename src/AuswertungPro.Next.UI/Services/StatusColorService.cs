using System;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Implementierung der zentralen Statusfarben. Kein Zugriff auf Application.Current,
/// damit die Klasse ohne laufende WPF-App testbar bleibt. Die Hex-Werte muessen mit
/// Theme.xaml / ThemeLight.xaml uebereinstimmen (StatusColorServiceTests sichern das ab).
/// </summary>
public sealed class StatusColorService : IStatusColorService
{
    // Severity 1..5 — Werte der Severity1..5Brush je Theme (Index 0 = Severity 1).
    private static readonly Color[] SeverityLight =
    {
        Color.FromRgb(0x16, 0xA3, 0x4A),
        Color.FromRgb(0x65, 0xA3, 0x0D),
        Color.FromRgb(0xF5, 0x9E, 0x0B),
        Color.FromRgb(0xEA, 0x58, 0x0C),
        Color.FromRgb(0xDC, 0x26, 0x26)
    };

    private static readonly Color[] SeverityDark =
    {
        Color.FromRgb(0x3F, 0xB9, 0x50),
        Color.FromRgb(0x84, 0xCC, 0x16),
        Color.FromRgb(0xD2, 0x99, 0x22),
        Color.FromRgb(0xF0, 0x88, 0x3E),
        Color.FromRgb(0xF8, 0x51, 0x49)
    };

    // Overlay-Severity (LiveFrame-Ring): gesaettigte Rampe, liegt ueber Video — nie theme-abhaengig.
    private static readonly Color[] SeverityOverlayRamp =
    {
        Color.FromRgb(34, 197, 94),
        Color.FromRgb(132, 204, 22),
        Color.FromRgb(245, 158, 11),
        Color.FromRgb(249, 115, 22),
        Color.FromRgb(239, 68, 68)
    };

    public Color Severity(int severity, string? theme = null)
    {
        var index = Math.Clamp(severity, 1, 5) - 1;
        return IsDark(theme) ? SeverityDark[index] : SeverityLight[index];
    }

    public Color SeverityOverlay(int severity)
        => SeverityOverlayRamp[Math.Clamp(severity, 1, 5) - 1];

    public Color Ampel(TrafficLight gate, string? theme = null)
    {
        var dark = IsDark(theme);
        return gate switch
        {
            TrafficLight.Green => dark ? Color.FromRgb(0x3F, 0xB9, 0x50) : Color.FromRgb(0x16, 0xA3, 0x4A),
            TrafficLight.Yellow => dark ? Color.FromRgb(0xD2, 0x99, 0x22) : Color.FromRgb(0xF5, 0x9E, 0x0B),
            TrafficLight.Red => dark ? Color.FromRgb(0xF8, 0x51, 0x49) : Color.FromRgb(0xDC, 0x26, 0x26),
            _ => Neutral(theme)
        };
    }

    public Color Outcome(AiDecisionOutcome? outcome, string? theme = null) => outcome switch
    {
        AiDecisionOutcome.AutoAccept => Ampel(TrafficLight.Green, theme),
        AiDecisionOutcome.Review => Ampel(TrafficLight.Yellow, theme),
        AiDecisionOutcome.Reject => Ampel(TrafficLight.Red, theme),
        _ => Neutral(theme)
    };

    public Color Confidence(double confidence, string? theme = null) => confidence switch
    {
        >= 0.85 => Ampel(TrafficLight.Green, theme),
        >= 0.60 => Ampel(TrafficLight.Yellow, theme),
        _ => Ampel(TrafficLight.Red, theme)
    };

    public Color Neutral(string? theme = null)
        => IsDark(theme) ? Color.FromRgb(0x88, 0x91, 0xA0) : Color.FromRgb(0x3D, 0x4D, 0x63);

    public Color OverlaySuccess => Player.PlayerStatusColors.Success;
    public Color OverlayWarning => Player.PlayerStatusColors.Warning;
    public Color OverlayError => Player.PlayerStatusColors.Error;
    public Color OverlayInfo => Player.PlayerStatusColors.Info;
    public Color OverlayMuted => Player.PlayerStatusColors.Muted;

    public Color? Zustandsklasse(string? klasse)
    {
        var key = ZustandsklasseColorPalette.NormalizeClass(klasse);
        return ZustandsklasseColorPalette.HaltungenPalette.TryGetValue(key, out var brush)
            && brush is SolidColorBrush solid
                ? solid.Color
                : null;
    }

    // theme = null -> aktives Theme; unbekannte Werte normalisiert der ThemeManager auf Hell.
    private static bool IsDark(string? theme)
        => ThemeManager.NormalizeTheme(theme ?? ThemeManager.CurrentTheme) == ThemeManager.Dark;
}
