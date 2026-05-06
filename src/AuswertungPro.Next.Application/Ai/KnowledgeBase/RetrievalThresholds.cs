namespace AuswertungPro.Next.Application.Ai.KnowledgeBase;

/// <summary>
/// Konfigurierbare Schwellen fuer KB-Retrieval (Embedding-Aehnlichkeit).
/// UI-AppSettings reicht beim DI-Bootstrap konkrete Werte ein.
/// </summary>
public sealed record RetrievalThresholds(
    double MinSimilarity = 0.35,
    double HybridSimilarity = 0.45);
