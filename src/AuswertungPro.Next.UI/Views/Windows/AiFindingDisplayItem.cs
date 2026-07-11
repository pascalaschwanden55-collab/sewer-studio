using System.Collections.Generic;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Display-Objekt fuer eine einzelne KI-Erkennung im Overlay / in der KI-Befundliste.
/// Aus CodingModeWindow.xaml.cs ausgelagert (das tote Fenster wurde entfernt) — dieser
/// Typ wird weiterhin von PlayerWindow.Coding genutzt.
/// </summary>
public sealed class AiFindingDisplayItem
{
    public AiFindingDisplayItem(LiveFrameFinding f)
    {
        Label = f.Label;
        // Gemeinsamer Resolver: VsaCodeHint normalisieren, bei Fehlschlag Label-Heuristik
        VsaCode = VsaCodeResolver.NormalizeFindingCode(f.VsaCodeHint)
                   ?? VsaCodeResolver.InferCodeFromLabel(f.Label)
                   ?? "";
        Severity = f.Severity;
        SeverityText = f.Severity.ToString();

        // VSA-Klartext aus Katalog (z.B. "BCAEB" → "Seitl. Anschluss, einmuendend, Bogen")
        Description = VsaCodeResolver.LookupLabel(VsaCode) ?? f.Label;

        // Position: Meter + Uhrzeit zusammengefasst
        var posParts = new List<string>();
        var normalizedClock = VsaCodeResolver.NormalizeClock(f.PositionClock);
        if (!string.IsNullOrWhiteSpace(normalizedClock))
            posParts.Add(normalizedClock);
        if (f.ExtentPercent.HasValue)
            posParts.Add($"{f.ExtentPercent}%");
        if (f.HeightMm is > 0)
            posParts.Add($"H:{f.HeightMm}mm");
        if (f.WidthMm is > 0)
            posParts.Add($"B:{f.WidthMm}mm");
        PositionText = posParts.Count > 0 ? string.Join(" · ", posParts) : "";

        // Detail-Text (fuer Tooltip und DetailPanel)
        var detailParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(normalizedClock))
            detailParts.Add($"Uhr {normalizedClock}");
        if (f.ExtentPercent.HasValue)
            detailParts.Add($"Umfang {f.ExtentPercent}%");
        if (f.HeightMm is > 0)
            detailParts.Add($"H:{f.HeightMm}mm");
        if (f.WidthMm is > 0)
            detailParts.Add($"B:{f.WidthMm}mm");
        if (f.IntrusionPercent is > 0)
            detailParts.Add($"Einragung {f.IntrusionPercent}%");
        DetailText = detailParts.Count > 0 ? string.Join("  |  ", detailParts) : "Keine Details";

        // Sicherheit nur aus ECHTEM Modellwert — Schadensgrad ist keine Confidence
        // (Fehlerpruefung 11.07., Kritisch 3: Severity*20 wurde als gruene Prozent-
        // Sicherheit angezeigt und konnte den Inspekteur direkt irrefuehren).
        ConfidencePercent = f.ModelConfidence is { } mc ? (int)System.Math.Round(mc * 100) : null;
        ConfidenceText = ConfidencePercent is { } p ? $"{p}%" : "Sicherheit: n/v";

        // Tooltip: Alles zusammen (Schadensgrad ausdruecklich getrennt ausgewiesen)
        FullTooltip = $"{VsaCode} {Description}\n{DetailText}\nSchadensgrad: {Severity}/5 · {ConfidenceText}";

        var severityColor = f.Severity switch
        {
            5 => Color.FromRgb(0xEF, 0x44, 0x44), // Rot (kritisch)
            4 => Color.FromRgb(0xF9, 0x73, 0x16), // Orange (schwer)
            3 => Color.FromRgb(0xF5, 0x9E, 0x0B), // Gelb (mittel)
            2 => Color.FromRgb(0x22, 0xC5, 0x5E), // Gruen (leicht)
            _ => Color.FromRgb(0x94, 0xA3, 0xB8)  // Grau (kaum)
        };
        SeverityBrush = new SolidColorBrush(severityColor);

        // Sicherheits-Farbe: Gruen >=85%, Gelb 60-85%, Rot <60% — ohne echten Wert GRAU
        // (neutral), niemals gruen fuer einen erfundenen Ersatzwert.
        ConfidenceBrush = new SolidColorBrush(ConfidencePercent switch
        {
            null => Color.FromRgb(0x94, 0xA3, 0xB8),
            >= 85 => Color.FromRgb(0x22, 0xC5, 0x5E),
            >= 60 => Color.FromRgb(0xF5, 0x9E, 0x0B),
            _ => Color.FromRgb(0xEF, 0x44, 0x44)
        });
    }

    public string Label { get; }
    public string VsaCode { get; }
    public string Description { get; }
    public int Severity { get; }
    public string SeverityText { get; }
    public string DetailText { get; }
    public string PositionText { get; }
    public int? ConfidencePercent { get; }
    public string ConfidenceText { get; }
    public string FullTooltip { get; }
    public SolidColorBrush SeverityBrush { get; }
    public SolidColorBrush ConfidenceBrush { get; }
}
