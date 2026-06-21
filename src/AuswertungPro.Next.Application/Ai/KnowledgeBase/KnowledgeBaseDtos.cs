using System;

namespace AuswertungPro.Next.Application.Ai.KnowledgeBase;

/// <param name="SampleId">Eindeutige ID des Samples.</param>
/// <param name="CaseId">Herkunft (TrainingCase).</param>
/// <param name="VsaCode">Zugehöriger VSA-Code.</param>
/// <param name="Beschreibung">Protokolltext.</param>
/// <param name="MeterStart">Meterposition Beginn.</param>
/// <param name="MeterEnd">Meterposition Ende.</param>
/// <param name="QualityGateLevel">QualityGate-Stufe ("Green"/"Yellow"/"Red", leer = unbekannt) — steuert die Retrieval-Gewichtung.</param>
/// <param name="HumanConfirmed">true = Mensch bestaetigt, false = abgelehnt, null = nie menschlich beurteilt (Audit Fix #3).</param>
/// <param name="Corrected">true = Mensch hat den KI-Code korrigiert, null = nie beurteilt (Audit Fix #3).</param>
/// <param name="ConfirmedByUser">Name des Bestaetigers. Null = unbekannt/alt (Audit Fix #3).</param>
/// <param name="ConfirmedAtUtc">UTC-Zeitpunkt der Bestaetigung. Null = unbekannt/alt (Audit Fix #3).</param>
public sealed record SampleRecord(
    string SampleId,
    string CaseId,
    string VsaCode,
    string Beschreibung,
    double MeterStart,
    double MeterEnd,
    string QualityGateLevel = "",
    bool? HumanConfirmed = null,
    bool? Corrected = null,
    string? ConfirmedByUser = null,
    DateTime? ConfirmedAtUtc = null);

/// <summary>Ein Retrieval-Ergebnis mit Ähnlichkeitswert.</summary>
public sealed record RetrievalResult(SampleRecord Sample, double Score);
