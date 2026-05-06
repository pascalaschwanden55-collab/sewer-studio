using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.Ai.Vision;

/// <summary>
/// Phase 5.3 vorbereitend: Output-Format der Vision-Analyse (pure Domain-Records).
/// Vorher in <c>UI.Ai.EnhancedVisionAnalysisService.cs</c> — Daten sind keine
/// UI-/Infrastructure-Belange, gehoeren in die Domain-Schicht.
/// </summary>
public sealed record EnhancedFrameAnalysis(
    double? Meter,
    string PipeMaterial,
    int? PipeDiameterMm,
    IReadOnlyList<EnhancedFinding> Findings,
    string ImageQuality,
    bool IsEmptyFrame,
    string? Error,
    string ViewType = "axial")
{
    public bool HasFindings => Findings.Count > 0;

    /// <summary>True wenn Axialsicht — nur dann sind Findings codierbar.</summary>
    public bool IsAxialView => ViewType == "axial" || ViewType == "schacht";

    /// <summary>True wenn Nahaufnahme oder Schwenk — Findings ignorieren.</summary>
    public bool IsNonCodableView => ViewType == "nahaufnahme" || ViewType == "schwenk";

    public static EnhancedFrameAnalysis Empty(string? error = null) =>
        new(null, "unbekannt", null,
            Array.Empty<EnhancedFinding>(), "schlecht", true, error, "axial");
}

/// <summary>
/// Einzelner KI-Befund aus der Vision-Analyse.
/// Severity 1-5; Position/Geometrie + optionale BBox aus DINO/SAM-Pipeline.
/// </summary>
public sealed record EnhancedFinding(
    string Label,
    string? VsaCodeHint,
    int Severity,         // 1-5
    string? PositionClock,
    int? ExtentPercent,
    int? HeightMm,
    int? WidthMm,
    int? IntrusionPercent,
    int? CrossSectionReductionPercent,
    int? DiameterReductionMm,
    string? Notes,
    // BBox normiert (0.0–1.0) — aus DINO/SAM Pipeline
    double? BboxX1Norm = null,
    double? BboxY1Norm = null,
    double? BboxX2Norm = null,
    double? BboxY2Norm = null,
    double? CentroidXNorm = null,
    double? CentroidYNorm = null
);
