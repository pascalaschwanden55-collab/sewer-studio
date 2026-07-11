using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Prueft die eine zentrale Farbquelle fuer Ampel/Severity/Konfidenz.
/// Theme wird IMMER explizit uebergeben (kein globaler Zustand im Test).
/// </summary>
public sealed class StatusColorServiceTests
{
    private readonly StatusColorService _svc = new();

    // ── Severity 1..5, theme-abhaengig (Werte = heutige Severity1..5Brush) ──

    [Fact]
    public void Severity_light_matches_theme_brushes()
    {
        Assert.Equal(Color.FromRgb(0x16, 0xA3, 0x4A), _svc.Severity(1, ThemeManager.Light));
        Assert.Equal(Color.FromRgb(0x65, 0xA3, 0x0D), _svc.Severity(2, ThemeManager.Light));
        Assert.Equal(Color.FromRgb(0xF5, 0x9E, 0x0B), _svc.Severity(3, ThemeManager.Light));
        Assert.Equal(Color.FromRgb(0xEA, 0x58, 0x0C), _svc.Severity(4, ThemeManager.Light));
        Assert.Equal(Color.FromRgb(0xDC, 0x26, 0x26), _svc.Severity(5, ThemeManager.Light));
    }

    [Fact]
    public void Severity_dark_matches_theme_brushes()
    {
        Assert.Equal(Color.FromRgb(0x3F, 0xB9, 0x50), _svc.Severity(1, ThemeManager.Dark));
        Assert.Equal(Color.FromRgb(0x84, 0xCC, 0x16), _svc.Severity(2, ThemeManager.Dark));
        Assert.Equal(Color.FromRgb(0xD2, 0x99, 0x22), _svc.Severity(3, ThemeManager.Dark));
        Assert.Equal(Color.FromRgb(0xF0, 0x88, 0x3E), _svc.Severity(4, ThemeManager.Dark));
        Assert.Equal(Color.FromRgb(0xF8, 0x51, 0x49), _svc.Severity(5, ThemeManager.Dark));
    }

    [Fact]
    public void Severity_clamps_out_of_range_values()
    {
        Assert.Equal(_svc.Severity(1, ThemeManager.Light), _svc.Severity(0, ThemeManager.Light));
        Assert.Equal(_svc.Severity(1, ThemeManager.Light), _svc.Severity(-3, ThemeManager.Light));
        Assert.Equal(_svc.Severity(5, ThemeManager.Light), _svc.Severity(7, ThemeManager.Light));
    }

    // ── Severity-Overlay (ueber Video/Frame, theme-unabhaengig, heutige LiveFrame-Werte) ──

    [Fact]
    public void SeverityOverlay_keeps_liveframe_ring_values()
    {
        Assert.Equal(Color.FromRgb(34, 197, 94), _svc.SeverityOverlay(1));
        Assert.Equal(Color.FromRgb(132, 204, 22), _svc.SeverityOverlay(2));
        Assert.Equal(Color.FromRgb(245, 158, 11), _svc.SeverityOverlay(3));
        Assert.Equal(Color.FromRgb(249, 115, 22), _svc.SeverityOverlay(4));
        Assert.Equal(Color.FromRgb(239, 68, 68), _svc.SeverityOverlay(5));
    }

    [Fact]
    public void SeverityOverlay_clamps_out_of_range_values()
    {
        Assert.Equal(_svc.SeverityOverlay(1), _svc.SeverityOverlay(0));
        Assert.Equal(_svc.SeverityOverlay(5), _svc.SeverityOverlay(99));
    }

    // ── QualityGate-Ampel: nutzt die Theme-Semantik (Success/Warning/Danger) ──

    [Fact]
    public void Ampel_light_uses_semantic_theme_colors()
    {
        Assert.Equal(Color.FromRgb(0x16, 0xA3, 0x4A), _svc.Ampel(TrafficLight.Green, ThemeManager.Light));
        Assert.Equal(Color.FromRgb(0xF5, 0x9E, 0x0B), _svc.Ampel(TrafficLight.Yellow, ThemeManager.Light));
        Assert.Equal(Color.FromRgb(0xDC, 0x26, 0x26), _svc.Ampel(TrafficLight.Red, ThemeManager.Light));
    }

    [Fact]
    public void Ampel_dark_uses_semantic_theme_colors()
    {
        Assert.Equal(Color.FromRgb(0x3F, 0xB9, 0x50), _svc.Ampel(TrafficLight.Green, ThemeManager.Dark));
        Assert.Equal(Color.FromRgb(0xD2, 0x99, 0x22), _svc.Ampel(TrafficLight.Yellow, ThemeManager.Dark));
        Assert.Equal(Color.FromRgb(0xF8, 0x51, 0x49), _svc.Ampel(TrafficLight.Red, ThemeManager.Dark));
    }

    // ── KI-Entscheidung (AutoAccept/Review/Reject) folgt der Ampel; null = Neutral ──

    [Fact]
    public void Outcome_maps_to_ampel_colors()
    {
        Assert.Equal(_svc.Ampel(TrafficLight.Green, ThemeManager.Light), _svc.Outcome(AiDecisionOutcome.AutoAccept, ThemeManager.Light));
        Assert.Equal(_svc.Ampel(TrafficLight.Yellow, ThemeManager.Light), _svc.Outcome(AiDecisionOutcome.Review, ThemeManager.Light));
        Assert.Equal(_svc.Ampel(TrafficLight.Red, ThemeManager.Light), _svc.Outcome(AiDecisionOutcome.Reject, ThemeManager.Light));
        Assert.Equal(_svc.Neutral(ThemeManager.Light), _svc.Outcome(null, ThemeManager.Light));
    }

    // ── Konfidenz-Schwellen: >=0.85 gruen, >=0.60 gelb, darunter rot ──

    [Fact]
    public void Confidence_thresholds_map_to_ampel()
    {
        Assert.Equal(_svc.Ampel(TrafficLight.Green, ThemeManager.Dark), _svc.Confidence(0.85, ThemeManager.Dark));
        Assert.Equal(_svc.Ampel(TrafficLight.Green, ThemeManager.Dark), _svc.Confidence(1.0, ThemeManager.Dark));
        Assert.Equal(_svc.Ampel(TrafficLight.Yellow, ThemeManager.Dark), _svc.Confidence(0.60, ThemeManager.Dark));
        Assert.Equal(_svc.Ampel(TrafficLight.Yellow, ThemeManager.Dark), _svc.Confidence(0.84, ThemeManager.Dark));
        Assert.Equal(_svc.Ampel(TrafficLight.Red, ThemeManager.Dark), _svc.Confidence(0.59, ThemeManager.Dark));
        Assert.Equal(_svc.Ampel(TrafficLight.Red, ThemeManager.Dark), _svc.Confidence(0.0, ThemeManager.Dark));
    }

    // ── Neutral (Muted je Theme) ──

    [Fact]
    public void Neutral_matches_muted_brush_per_theme()
    {
        Assert.Equal(Color.FromRgb(0x3D, 0x4D, 0x63), _svc.Neutral(ThemeManager.Light));
        Assert.Equal(Color.FromRgb(0x88, 0x91, 0xA0), _svc.Neutral(ThemeManager.Dark));
    }

    // ── Overlay-Palette (Player/Video): identisch zu PlayerStatusColors ──

    [Fact]
    public void Overlay_palette_matches_player_status_colors()
    {
        Assert.Equal(Player.PlayerStatusColors.Success, _svc.OverlaySuccess);
        Assert.Equal(Player.PlayerStatusColors.Warning, _svc.OverlayWarning);
        Assert.Equal(Player.PlayerStatusColors.Error, _svc.OverlayError);
        Assert.Equal(Player.PlayerStatusColors.Info, _svc.OverlayInfo);
        Assert.Equal(Player.PlayerStatusColors.Muted, _svc.OverlayMuted);
    }

    // ── Zustandsklasse (Excel-Palette, fachlich fix) ──

    [Fact]
    public void Zustandsklasse_delegates_to_excel_palette()
    {
        Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), _svc.Zustandsklasse("0"));
        Assert.Equal(Color.FromRgb(0xFF, 0x66, 0x00), _svc.Zustandsklasse("1"));
        Assert.Equal(Color.FromRgb(0xFF, 0xFF, 0x00), _svc.Zustandsklasse("2"));
        Assert.Equal(Color.FromRgb(0xAE, 0xB1, 0x35), _svc.Zustandsklasse("3"));
        Assert.Equal(Color.FromRgb(0x92, 0xD0, 0x50), _svc.Zustandsklasse("4"));
        Assert.Null(_svc.Zustandsklasse("9"));
        Assert.Null(_svc.Zustandsklasse(null));
    }

    [Fact]
    public void Zustandsklasse_normalizes_decimal_input()
    {
        // Nutzt dieselbe Normalisierung wie die Tabelle (Komma-Dezimale, Rundung).
        Assert.Equal(Color.FromRgb(0xFF, 0xFF, 0x00), _svc.Zustandsklasse("2,4"));
    }

    // ── Unbekanntes Theme faellt auf Hell (Default) zurueck ──

    [Fact]
    public void Unknown_theme_falls_back_to_light()
    {
        Assert.Equal(_svc.Severity(3, ThemeManager.Light), _svc.Severity(3, "Quatsch"));
        Assert.Equal(_svc.Severity(3, ThemeManager.Light), _svc.Severity(3, null));
    }
}
