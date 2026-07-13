using System.Net.Http;

namespace AuswertungPro.Next.Application.Ai;

public interface IVideoAnalysisPipelineFactory
{
    IVideoAnalysisPipelineService Create(
        AiRuntimeSettings settings,
        IAiSuggestionPlausibilityService plausibility,
        HttpClient httpClient);
}
