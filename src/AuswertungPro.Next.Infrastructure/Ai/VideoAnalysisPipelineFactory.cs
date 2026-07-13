using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Ai;

public sealed class VideoAnalysisPipelineFactory : IVideoAnalysisPipelineFactory
{
    private readonly Func<PipelineConfig> _getPipelineConfig;
    private readonly ICodeCatalogProvider? _codeCatalog;
    private readonly ILoggerFactory _loggerFactory;

    public VideoAnalysisPipelineFactory(
        Func<PipelineConfig> getPipelineConfig,
        ICodeCatalogProvider? codeCatalog = null,
        ILoggerFactory? loggerFactory = null)
    {
        _getPipelineConfig = getPipelineConfig ?? throw new ArgumentNullException(nameof(getPipelineConfig));
        _codeCatalog = codeCatalog;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public IVideoAnalysisPipelineService Create(
        AiRuntimeSettings settings,
        IAiSuggestionPlausibilityService plausibility,
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(plausibility);
        ArgumentNullException.ThrowIfNull(httpClient);

        return new VideoAnalysisPipelineService(
            settings,
            _getPipelineConfig(),
            plausibility,
            httpClient,
            _codeCatalog,
            _loggerFactory);
    }
}
