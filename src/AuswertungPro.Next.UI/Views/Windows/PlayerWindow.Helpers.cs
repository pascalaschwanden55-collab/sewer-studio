using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.A: Pure-Function-Helpers aus PlayerWindow.xaml.cs.
// Cluster B8 (Cross-Cutting Helpers) — kein Field-/UI-Zugriff,
// nur Math/String/Domain-Konstanten. Sichere Verschiebung.
public partial class PlayerWindow
{
    private static string FormatMs(long ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1 ? t.ToString(@"hh\:mm\:ss") : t.ToString(@"mm\:ss");
    }

    private static Color SeverityToColor(int severity, bool hasDamage)
    {
        if (!hasDamage)
            return Color.FromArgb(100, 0x94, 0xA3, 0xB8); // grey with alpha

        return severity switch
        {
            >= 4 => (Color)ColorConverter.ConvertFromString("#EF4444"), // red
            3    => (Color)ColorConverter.ConvertFromString("#F59E0B"), // orange
            2    => (Color)ColorConverter.ConvertFromString("#FACC15"), // yellow
            _    => (Color)ColorConverter.ConvertFromString("#22C55E"), // green
        };
    }

    private static string CompactModelName(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return "?";

        var trimmed = model.Trim();
        var slashIndex = trimmed.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < trimmed.Length - 1)
            trimmed = trimmed[(slashIndex + 1)..];
        return trimmed;
    }

    private static Color MapDetectionSeverityColor(int severity) => Math.Clamp(severity, 1, 5) switch
    {
        >= 5 => Color.FromRgb(239, 68, 68),
        4 => Color.FromRgb(249, 115, 22),
        3 => Color.FromRgb(245, 158, 11),
        2 => Color.FromRgb(132, 204, 22),
        _ => Color.FromRgb(34, 197, 94)
    };

    private static int? ParseClockHour(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var m = Regex.Match(raw, @"\b(?<h>1[0-2]|0?[1-9])\b");
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups["h"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
            return null;
        if (hour == 0) return 12;
        if (hour > 12) hour %= 12;
        return hour == 0 ? 12 : hour;
    }

    private static Geometry BuildRingSectorGeometry(
        double cx, double cy, double innerR, double outerR, double startDeg, double sweepDeg)
    {
        var startRad = DegToRad(startDeg);
        var endRad = DegToRad(startDeg + sweepDeg);
        var large = sweepDeg > 180;

        var p1 = new Point(cx + Math.Cos(startRad) * outerR, cy + Math.Sin(startRad) * outerR);
        var p2 = new Point(cx + Math.Cos(endRad) * outerR, cy + Math.Sin(endRad) * outerR);
        var p3 = new Point(cx + Math.Cos(endRad) * innerR, cy + Math.Sin(endRad) * innerR);
        var p4 = new Point(cx + Math.Cos(startRad) * innerR, cy + Math.Sin(startRad) * innerR);

        var fig = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
        fig.Segments.Add(new ArcSegment(p2, new Size(outerR, outerR), 0, large, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(p3, true));
        fig.Segments.Add(new ArcSegment(p4, new Size(innerR, innerR), 0, large, SweepDirection.Counterclockwise, true));
        return new PathGeometry(new[] { fig });
    }

    private static double DegToRad(double deg) => deg * Math.PI / 180.0;

    private static string TrimStatus(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "Unbekannter Fehler";
        message = message.Replace(Environment.NewLine, " ").Trim();
        return message.Length > 120 ? message[..120] : message;
    }

    private static string MakeRejectionKey(string? vsaCode, double meter)
        => $"{vsaCode ?? "?"}@{Math.Round(meter * 2) / 2:F1}";  // Auf 0.5m runden

    private static bool IsAllowedImportFallbackCode(string code)
        => code.StartsWith("BCD", StringComparison.OrdinalIgnoreCase)   // Rohranfang
           || code.StartsWith("BCE", StringComparison.OrdinalIgnoreCase) // Rohrende
           || code.StartsWith("BCA", StringComparison.OrdinalIgnoreCase) // Seitl. Anschluss
           || code.StartsWith("BCC", StringComparison.OrdinalIgnoreCase) // Bogen
           || code.StartsWith("BBC", StringComparison.OrdinalIgnoreCase) // Ablagerung
           || code.StartsWith("BDD", StringComparison.OrdinalIgnoreCase) // Wasserspiegel
           // Strukturschaeden (BA-Gruppe)
           || code.StartsWith("BAA", StringComparison.OrdinalIgnoreCase) // Verformung
           || code.StartsWith("BAB", StringComparison.OrdinalIgnoreCase) // Riss
           || code.StartsWith("BAC", StringComparison.OrdinalIgnoreCase) // Bruch
           || code.StartsWith("BAF", StringComparison.OrdinalIgnoreCase) // Oberflaechenschaden
           || code.StartsWith("BAG", StringComparison.OrdinalIgnoreCase) // Einragender Anschluss
           || code.StartsWith("BAH", StringComparison.OrdinalIgnoreCase) // Schadhafter Anschluss
           || code.StartsWith("BAI", StringComparison.OrdinalIgnoreCase) // Einragendes Dichtungsmaterial
           || code.StartsWith("BAJ", StringComparison.OrdinalIgnoreCase) // Versatz
           // Betriebliche Stoerungen (BB-Gruppe)
           || code.StartsWith("BBA", StringComparison.OrdinalIgnoreCase) // Wurzeln
           || code.StartsWith("BBB", StringComparison.OrdinalIgnoreCase) // Anhaftende Stoffe (Inkrustation)
           || code.StartsWith("BBD", StringComparison.OrdinalIgnoreCase) // Eindringender Boden
           || code.StartsWith("BBF", StringComparison.OrdinalIgnoreCase); // Infiltration
}
