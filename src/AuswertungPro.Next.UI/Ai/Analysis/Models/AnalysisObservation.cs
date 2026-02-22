// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using AuswertungPro.Next.UI.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Analysis.Models;

/// <summary>
/// Eine durch die Analyse-Pipeline erzeugte Beobachtung mit VSA-Code,
/// Protokolltext, Quantifizierung und differenzierter Konfidenz.
/// </summary>
public sealed class AnalysisObservation
{
    /// <summary>Eindeutige ID dieser Beobachtung (für UI-Tracking).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>VSA-Code (z.B. "BAB").</summary>
    public required string VsaCode { get; set; }

    /// <summary>Charakterisierung A–D (null wenn nicht zutreffend).</summary>
    public string? Characterization { get; set; }

    /// <summary>Kurztitel (z.B. "Rissbildung").</summary>
    public required string Label { get; set; }

    /// <summary>Normkonformer Protokolltext in Fachdeutsch.</summary>
    public required string Text { get; set; }

    /// <summary>Strukturierte Quantifizierung (null wenn nicht vorhanden).</summary>
    public QuantificationDetail? Quantification { get; set; }

    /// <summary>Differenzierte Konfidenz.</summary>
    public required ObservationConfidence Confidence { get; set; }

    /// <summary>Begründung der KI-Entscheidung (für Human-in-the-Loop).</summary>
    public string Evidence { get; set; } = "";

    /// <summary>True wenn Streckenschaden (MeterEnd > MeterStart + 0.1).</summary>
    public bool IsStreckenschaden { get; set; }

    /// <summary>Meter-Position Beginn.</summary>
    public double MeterStart { get; set; }

    /// <summary>Meter-Position Ende (= MeterStart bei Einzelschaden).</summary>
    public double MeterEnd { get; set; }

    /// <summary>Validierungsfehler (leer = alles ok).</summary>
    public List<string> ValidationFlags { get; set; } = [];

    /// <summary>True wenn der Nutzer diese Beobachtung akzeptiert hat.</summary>
    public bool IsApproved { get; set; }

    /// <summary>True wenn der Nutzer diese Beobachtung abgelehnt hat.</summary>
    public bool IsRejected { get; set; }
}
