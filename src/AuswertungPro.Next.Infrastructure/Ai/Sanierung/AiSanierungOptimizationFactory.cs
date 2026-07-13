using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Sanierung;

namespace AuswertungPro.Next.Infrastructure.Ai.Sanierung;

public sealed class AiSanierungOptimizationFactory : IAiSanierungOptimizationFactory
{
    public IAiSanierungOptimizationService Create(
        AiRuntimeSettings settings,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new AiSanierungOptimizationService(settings, httpClient);
    }
}
