namespace AuswertungPro.Next.Application.Ai.Sanierung;

public interface IAiSanierungOptimizationService
{
    Task<SanierungOptimizationResult> OptimizeAsync(SanierungOptimizationRequest req, CancellationToken ct);
}
