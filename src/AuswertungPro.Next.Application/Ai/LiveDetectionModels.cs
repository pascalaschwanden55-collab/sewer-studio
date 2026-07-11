namespace AuswertungPro.Next.Application.Ai;

public sealed record LiveDetection(
    double TimestampSeconds,
    IReadOnlyList<LiveFrameFinding> Findings,
    double? MeterReading,
    string? Error,
    AnalysisOutcome Outcome = AnalysisOutcome.Ok);

public sealed record LiveFrameFinding(
    string Label,
    int Severity,
    string? PositionClock,
    int? ExtentPercent,
    string? VsaCodeHint = null,
    int? HeightMm = null,
    int? WidthMm = null,
    int? IntrusionPercent = null,
    int? CrossSectionReductionPercent = null,
    int? DiameterReductionMm = null,
    double? BboxX1 = null,
    double? BboxY1 = null,
    double? BboxX2 = null,
    double? BboxY2 = null,
    // Echte Modell-Sicherheit (0..1) — NICHT der Schadensgrad! Null = kein echter Wert;
    // dann zeigt die UI "n/v" und das QualityGate bekommt KEIN Ersatzsignal
    // (Fehlerpruefung 11.07., Kritisch 3: Severity/5 war als QwenVisionConf getarnt).
    double? ModelConfidence = null,
    // Herkunft des Werts (z.B. "qwen", "dino"), fuer Nachvollziehbarkeit.
    string? ConfidenceSource = null);
