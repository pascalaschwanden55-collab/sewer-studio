namespace AuswertungPro.Next.Application.Ai.KnowledgeBase;

/// <param name="SampleId">Eindeutige ID des Samples.</param>
/// <param name="CaseId">Herkunft (TrainingCase).</param>
/// <param name="VsaCode">Zugehöriger VSA-Code.</param>
/// <param name="Beschreibung">Protokolltext.</param>
/// <param name="MeterStart">Meterposition Beginn.</param>
/// <param name="MeterEnd">Meterposition Ende.</param>
public sealed record SampleRecord(
    string SampleId,
    string CaseId,
    string VsaCode,
    string Beschreibung,
    double MeterStart,
    double MeterEnd);

/// <summary>Ein Retrieval-Ergebnis mit Ähnlichkeitswert.</summary>
public sealed record RetrievalResult(SampleRecord Sample, double Score);
