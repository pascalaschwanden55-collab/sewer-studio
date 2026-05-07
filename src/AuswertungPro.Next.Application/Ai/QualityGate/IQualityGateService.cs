namespace AuswertungPro.Next.Application.Ai.QualityGate;

/// <summary>
/// Audit 2026-04-23 ARCH-H3: Interface fuer das QualityGate. Erlaubt
/// Mock-basierte Unit-Tests (TDD) fuer Konsumenten — ohne dass die
/// Tests den realen Weight-Fusion-Algorithmus mit-ausfuehren muessen.
///
/// Implementierung: <see cref="QualityGateService"/>.
/// </summary>
public interface IQualityGateService
{
    /// <summary>
    /// Bewertet eine Evidenz-Aussage (8 Signale aus YOLO/DINO/SAM/Qwen/LLM/KB/Plausibility).
    /// Liefert Composite-Confidence + Ampel + verwendete Gewichte.
    /// </summary>
    QualityGateResult Evaluate(EvidenceVector evidence);

    /// <summary>Setzt/ueberschreibt Gewichte fuer eine Schadens-Kategorie.</summary>
    void SetWeights(CategoryWeights weights);
}
