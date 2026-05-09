using System;
using System.Linq;

using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

// CodingModeWindow Dedup- und Material-Heuristik-Helfer (Slice 8a.2.1).
// Reine Logik-Helfer ohne UI-Bindung. Migriert aus
// PlayerWindow.CodingMode.cs damit der Live-AI-Coding-Loop spaeter
// vollstaendig im CodingModeWindow leben kann (Slice 8a.7).
public partial class CodingModeWindow
{
    /// <summary>
    /// Prueft ob die aktuelle Haltung ein Kunststoffrohr hat (PE, PVC, PP, GFK).
    /// Kunststoffrohre sind dicht — Infiltration nur bei Begleitschaden moeglich.
    /// </summary>
    private bool IsKunststoffRohr()
    {
        var material = _haltung?.GetFieldValue("Rohrmaterial") ?? "";
        if (string.IsNullOrWhiteSpace(material)) return false;
        var m = material.ToUpperInvariant();
        return m.Contains("PE") || m.Contains("PVC") || m.Contains("PP")
            || m.Contains("GFK") || m.Contains("KUNSTSTOFF") || m.Contains("PLASTIK")
            || m.Contains("POLYETHYL") || m.Contains("POLYPROP") || m.Contains("POLYVINYL")
            || m.Contains("HDPE") || m.Contains("FASERZ");
    }

    /// <summary>
    /// Prueft ob in der Naehe (±2m) ein Strukturschaden (BA-Code) existiert.
    /// BA = Riss, Bruch, Deformation, Versatz, defekte Verbindung.
    /// Wenn ja, ist Infiltration auch bei Kunststoff plausibel.
    /// </summary>
    private bool HasNearbyStructuralDamage(double meter)
    {
        if (_vm == null) return false;
        return _vm.Events.Any(e =>
        {
            var evCode = e.Entry.Code;
            if (string.IsNullOrEmpty(evCode) || evCode.Length < 2) return false;
            var prefix = evCode[..2].ToUpperInvariant();
            return prefix == "BA" && Math.Abs(e.MeterAtCapture - meter) < 2.0;
        });
    }

    /// <summary>
    /// Prueft ob ein neuer Fund bereits durch ein bestehendes Event abgedeckt ist.
    /// Beruecksichtigt: Streckenschaeden (ganzer Bereich), akzeptierte Events,
    /// und Punktschaeden (±0.3m + Position).
    /// </summary>
    private static bool IsAlreadyCovered(CodingEvent existing, double newMeter, LiveFrameFinding newFinding)
    {
        // Einmal-Codes: BCD (Rohranfang), BCE (Rohrende), BDC (Abbruch) duerfen
        // nur 1× pro Session vorkommen — Meter-Distanz ist irrelevant
        var existBaseCode = existing.Entry.Code?.Length >= 3
            ? existing.Entry.Code[..3].ToUpperInvariant() : "";
        if (existBaseCode is "BCD" or "BCE" or "BDC")
            return true; // IMMER Duplikat, egal bei welchem Meter

        // Streckenschaden: der ganze Bereich MeterStart..MeterEnd ist abgedeckt
        if (existing.Entry.IsStreckenschaden)
        {
            var start = existing.Entry.MeterStart ?? existing.MeterAtCapture;
            var end = existing.Entry.MeterEnd ?? double.MaxValue; // offen = bis Ende
            return newMeter >= (start - 0.1) && newMeter <= (end + 0.1);
        }

        // Bereits akzeptiertes/bearbeitetes Event: gleicher Code innerhalb ±1.0m
        // nicht nochmal melden (User hat den Schaden schon gesehen und bestaetigt)
        if (existing.AiContext?.Decision is CodingUserDecision.Accepted
            or CodingUserDecision.AcceptedWithEdit)
        {
            return Math.Abs(existing.MeterAtCapture - newMeter) < 1.0;
        }

        // Punktschaden: gleicher Code innerhalb ±1.0m
        if (Math.Abs(existing.MeterAtCapture - newMeter) >= 1.0)
            return false;

        // BCA (Anschluss) kann mehrfach am gleichen Meter vorkommen (z.B. 3h und 9h)
        // → Position-Check noetig um verschiedene Anschluesse zu unterscheiden
        var baseCode = newFinding.VsaCodeHint?.Length >= 3
            ? newFinding.VsaCodeHint[..3].ToUpperInvariant() : "";
        if (baseCode == "BCA")
            return IsSamePosition(existing, newFinding);

        // Alle anderen Codes: gleicher Meter = Duplikat (kein Position-Check noetig)
        return true;
    }

    /// <summary>
    /// Positionsvergleich fuer Duplikat-Erkennung.
    /// Zwei Befunde mit gleichem Code gelten als gleiche Position wenn:
    /// - Beide BBox haben → Mittelpunktabstand kleiner 15% (normalisiert)
    /// - Keiner BBox hat → gleiche Uhrlage
    /// - Gemischt (BBox vs. ohne) → Uhrlage vergleichen als Fallback.
    ///   Verhindert Duplikate wenn Vision die BBox mal liefert, mal nicht.
    /// </summary>
    private static bool IsSamePosition(CodingEvent existing, LiveFrameFinding newFinding)
    {
        bool newHasBbox = newFinding.BboxX1.HasValue && newFinding.BboxY1.HasValue
                       && newFinding.BboxX2.HasValue && newFinding.BboxY2.HasValue;
        bool existHasBbox = existing.Overlay?.Points?.Count >= 4;

        if (newHasBbox && existHasBbox)
        {
            // Mittelpunkt-Vergleich (normalisierte Koordinaten 0..1)
            var ncx = (newFinding.BboxX1!.Value + newFinding.BboxX2!.Value) / 2;
            var ncy = (newFinding.BboxY1!.Value + newFinding.BboxY2!.Value) / 2;
            var pts = existing.Overlay!.Points;
            var ecx = (pts[0].X + pts[2].X) / 2;
            var ecy = (pts[0].Y + pts[2].Y) / 2;
            var dist = Math.Sqrt(Math.Pow(ncx - ecx, 2) + Math.Pow(ncy - ecy, 2));
            return dist < 0.15;
        }

        // Fallback: Uhrlage vergleichen (auch bei gemischtem BBox-Status).
        // Faengt den Fall ab, dass Vision die BBox mal liefert und mal nicht.
        var existClock = existing.Entry.CodeMeta?.Parameters
            ?.GetValueOrDefault("vsa.uhr.von");
        var newClock = newFinding.PositionClock;

        // Beide haben Uhrlage → vergleichen
        if (!string.IsNullOrEmpty(existClock) && !string.IsNullOrEmpty(newClock))
            return string.Equals(existClock, newClock, StringComparison.OrdinalIgnoreCase);

        // Keine Positionsinfo verfuegbar → konservativ: als gleich werten (Duplikat annehmen)
        return true;
    }

    /// <summary>
    /// Prueft ob zwei VSA-Codes fuer Dedup-Zwecke als gleich gelten.
    /// Exakter Match ODER gleicher 3-Zeichen-Hauptcode (z.B. BCAEB vs BCA).
    /// </summary>
    private static bool CodesMatchForDedup(string? existingCode, string newCode)
    {
        if (string.IsNullOrWhiteSpace(existingCode) || string.IsNullOrWhiteSpace(newCode))
            return false;

        // Exakter Match
        if (string.Equals(existingCode, newCode, StringComparison.OrdinalIgnoreCase))
            return true;

        // Hauptcode-Match: gleicher 3-Zeichen-Prefix = gleiche Schadensgruppe
        if (existingCode.Length >= 3 && newCode.Length >= 3)
            return string.Equals(
                existingCode[..3], newCode[..3], StringComparison.OrdinalIgnoreCase);

        return false;
    }
}
