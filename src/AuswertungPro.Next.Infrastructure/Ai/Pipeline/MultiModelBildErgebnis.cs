using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Ausgang eines einzelnen Bildes der Mehrmodell-Analyse. Der Bildschritt legt nur fest, WAS
/// mit dem Bild geschah; der aeussere Ablauf bucht daraus Trace, Zusammenfuehrung (Dedup),
/// Checkpoint und Fehlerzaehlung in der belegten Reihenfolge und entscheidet ueber die
/// Fortsetzung. Einen Abbruch meldet der Bildschritt nie selbst: Ein Nutzerabbruch ist eine
/// Ausnahme, und ueber den Abbruch wegen Sidecar-Ausfalls entscheidet erst die Fehlerzaehlung.
/// </summary>
internal enum MultiModelBildAusgang
{
    /// <summary>Regulaer uebersprungen (leeres Bild, Vorfilter, YOLO irrelevant): kein Befund, kein Fehler.</summary>
    Uebersprungen,

    /// <summary>DINO fand keine Box: sauberer Negativbefund; der Trace folgt hier NACH dem Checkpoint.</summary>
    OhneBoxUebersprungen,

    /// <summary>DINO ohne Box, aber vom Klassifikator bestaetigter Grundgeruest-Code (box-loser Befund).</summary>
    Grundgeruestbefund,

    /// <summary>SAM-Masken ausgewertet: Befunde (auch keine); bei technischem Teilverlust erneut noetig.</summary>
    Befunde,

    /// <summary>Technischer Fehler oder Modellfehler: das Bild muss spaeter erneut bearbeitet werden.</summary>
    ErneutNoetig,
}

/// <summary>Art des technischen Fehlers; die Regeln unterscheiden sich bewusst je Art.</summary>
internal enum MultiModelFehlerart
{
    Keine,

    /// <summary>Sidecar-Transportfehler (auch interner Timeout): zaehlt in die Ausfallserie, darf Neustart/Abbruch ausloesen.</summary>
    Transport,

    /// <summary>VRAM-Mangel: nur Skip-Quote, nie Ausfallserie oder Neustart; Meldung wird Degraded-Grund.</summary>
    Kapazitaet,

    /// <summary>Modell meldet eingeschraenkte Antwort (z. B. DINO degraded): nur Skip-Quote.</summary>
    Modell,
}

internal sealed record MultiModelBildErgebnis(
    MultiModelBildAusgang Ausgang,
    double Meter,
    IReadOnlyList<EnhancedFinding> Befunde,
    EvidenceVector? Evidence = null,
    string? MeterSource = null,
    bool IsMeterEstimated = true,
    bool ErneutNoetig = false,
    MultiModelFehlerart Fehlerart = MultiModelFehlerart.Keine,
    string? FehlerCode = null,
    string? KapazitaetMeldung = null,
    byte[]? FrameBytes = null)
{
    public static MultiModelBildErgebnis Uebersprungen(double meter)
        => new(MultiModelBildAusgang.Uebersprungen, meter, Array.Empty<EnhancedFinding>());

    public static MultiModelBildErgebnis OhneBox(double meter)
        => new(MultiModelBildAusgang.OhneBoxUebersprungen, meter, Array.Empty<EnhancedFinding>());

    public static MultiModelBildErgebnis Transportfehler(string fehlerCode, double meter)
        => new(MultiModelBildAusgang.ErneutNoetig, meter, Array.Empty<EnhancedFinding>(),
            Fehlerart: MultiModelFehlerart.Transport, FehlerCode: fehlerCode);

    public static MultiModelBildErgebnis VramMangel(string meldung, double meter)
        => new(MultiModelBildAusgang.ErneutNoetig, meter, Array.Empty<EnhancedFinding>(),
            Fehlerart: MultiModelFehlerart.Kapazitaet, KapazitaetMeldung: meldung);

    public static MultiModelBildErgebnis Modellfehler(double meter)
        => new(MultiModelBildAusgang.ErneutNoetig, meter, Array.Empty<EnhancedFinding>(),
            Fehlerart: MultiModelFehlerart.Modell);

    /// <summary>Befunde des Bildes fuer die Live-Anzeige im Fortschritt.</summary>
    public List<LiveFrameFinding> LiveFindings()
        => Befunde.Select(f => new LiveFrameFinding(
            Label: f.Label,
            Severity: f.Severity,
            PositionClock: f.PositionClock,
            ExtentPercent: f.ExtentPercent,
            VsaCodeHint: f.VsaCodeHint,
            HeightMm: f.HeightMm,
            WidthMm: f.WidthMm,
            IntrusionPercent: f.IntrusionPercent,
            CrossSectionReductionPercent: f.CrossSectionReductionPercent,
            DiameterReductionMm: f.DiameterReductionMm
        )).ToList();
}
