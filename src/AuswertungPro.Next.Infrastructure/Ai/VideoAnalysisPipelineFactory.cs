using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Ai;

public sealed class VideoAnalysisPipelineFactory : IVideoAnalysisPipelineFactory
{
    private readonly Func<PipelineConfig> _getPipelineConfig;
    private readonly ICodeCatalogProvider? _codeCatalog;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPipelineTraceWriter _pipelineTraceWriter;

    public VideoAnalysisPipelineFactory(
        Func<PipelineConfig> getPipelineConfig,
        ICodeCatalogProvider? codeCatalog = null,
        ILoggerFactory? loggerFactory = null)
        : this(PipelineTraceWriter.Current, getPipelineConfig, codeCatalog, loggerFactory)
    {
    }

    public VideoAnalysisPipelineFactory(
        IPipelineTraceWriter pipelineTraceWriter,
        Func<PipelineConfig> getPipelineConfig,
        ICodeCatalogProvider? codeCatalog = null,
        ILoggerFactory? loggerFactory = null)
    {
        _pipelineTraceWriter = pipelineTraceWriter ?? throw new ArgumentNullException(nameof(pipelineTraceWriter));
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
            _pipelineTraceWriter,
            settings,
            _getPipelineConfig(),
            plausibility,
            httpClient,
            _codeCatalog,
            _loggerFactory);
    }
}
