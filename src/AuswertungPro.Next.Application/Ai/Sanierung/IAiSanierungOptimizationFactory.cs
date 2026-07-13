using System.Net.Http;

namespace AuswertungPro.Next.Application.Ai.Sanierung;

public interface IAiSanierungOptimizationFactory
{
    IAiSanierungOptimizationService Create(
        AiRuntimeSettings settings,
        HttpClient? httpClient = null);
}
