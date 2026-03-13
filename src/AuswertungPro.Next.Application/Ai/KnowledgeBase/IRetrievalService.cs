namespace AuswertungPro.Next.Application.Ai.KnowledgeBase;

public interface IRetrievalService
{
    Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(string queryText, int topK = 5, CancellationToken ct = default);
    bool CheckModelConsistency();
    string? StoredEmbedModel { get; }
    bool HasModelMismatch { get; }
}
