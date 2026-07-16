using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
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
    private readonly IProcessOutputReader _processOutputs;
    private readonly IPipelineEnvironmentOptions _pipelineEnvironmentOptions;
    private readonly ISidecarTelemetryWriter _sidecarTelemetry;

    public VideoAnalysisPipelineFactory(
        Func<PipelineConfig> getPipelineConfig,
        ICodeCatalogProvider? codeCatalog = null,
        ILoggerFactory? loggerFactory = null,
        IPipelineEnvironmentOptions? pipelineEnvironmentOptions = null,
        ISidecarTelemetryWriter? sidecarTelemetry = null)
        : this(
            PipelineTraceWriter.Current,
            ProcessOutputReader.Current,
            getPipelineConfig,
            codeCatalog,
            loggerFactory,
            pipelineEnvironmentOptions,
            sidecarTelemetry)
    {
    }

    public VideoAnalysisPipelineFactory(
        IPipelineTraceWriter pipelineTraceWriter,
        Func<PipelineConfig> getPipelineConfig,
        ICodeCatalogProvider? codeCatalog = null,
        ILoggerFactory? loggerFactory = null,
        IPipelineEnvironmentOptions? pipelineEnvironmentOptions = null,
        ISidecarTelemetryWriter? sidecarTelemetry = null)
        : this(
            pipelineTraceWriter,
            ProcessOutputReader.Current,
            getPipelineConfig,
            codeCatalog,
            loggerFactory,
            pipelineEnvironmentOptions,
            sidecarTelemetry)
    {
    }

    public VideoAnalysisPipelineFactory(
        IPipelineTraceWriter pipelineTraceWriter,
        IProcessOutputReader processOutputs,
        Func<PipelineConfig> getPipelineConfig,
        ICodeCatalogProvider? codeCatalog = null,
        ILoggerFactory? loggerFactory = null,
        IPipelineEnvironmentOptions? pipelineEnvironmentOptions = null,
        ISidecarTelemetryWriter? sidecarTelemetry = null)
    {
        _pipelineTraceWriter = pipelineTraceWriter ?? throw new ArgumentNullException(nameof(pipelineTraceWriter));
        _processOutputs = processOutputs ?? throw new ArgumentNullException(nameof(processOutputs));
        _pipelineEnvironmentOptions = pipelineEnvironmentOptions ?? PipelineEnvironmentOptions.Current;
        _sidecarTelemetry = sidecarTelemetry ?? SidecarTelemetryWriter.Current;
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
            _processOutputs,
            settings,
            _getPipelineConfig(),
            plausibility,
            httpClient,
            _codeCatalog,
            _loggerFactory,
            _pipelineEnvironmentOptions,
            _sidecarTelemetry);
    }
}
