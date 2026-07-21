using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingAiController : IDisposable
{
    private CodingAiRuntime? _runtime;
    private CancellationTokenSource? _analysisCancellation;
    private IPipelineHealthMonitor? _healthMonitor;
    private EventHandler<PipelineHealthStatus>? _healthStatusChanged;

    public LiveDetectionService? LiveDetection { get; private set; }
    public EnhancedVisionAnalysisService? EnhancedVision { get; private set; }
    public GuidedVerificationService? ProtocolVerifier { get; private set; }
    public CancellationTokenSource? AnalysisCancellation => _analysisCancellation;
    public bool IsAnalyzing { get; private set; }
    public string ModelName { get; private set; } = string.Empty;
    public QualityGateService? QualityGate { get; private set; }
    public SingleFrameMultiModelService? MultiModel { get; private set; }
    public IVisionPipelineClient? VisionClient { get; private set; }
    public MarkBoxSegmentationService? BoxSegmentation { get; private set; }
    public PipelineConfig? PipelineConfig { get; private set; }
    public bool UseMultiModel { get; private set; }
    public bool AiEnabled { get; private set; }
    public bool HasHealthMonitor => _healthMonitor is not null;
    public bool QwenAvailable => LiveDetection is not null || EnhancedVision is not null;
    public bool CanAnalyze => LiveDetection is not null || EnhancedVision is not null || MultiModel is not null;

    public void ApplyRuntime(CodingAiRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        // Eine zuvor angewandte Runtime hielt einen eigenen VisionClient/OllamaClient. Vor dem
        // Setzen einer neuen wird die alte freigegeben — sonst leakt jeder Codiermodus-
        // Wiedereintritt zwei HttpClients. Eine laufende Analyse wird zuvor abgebrochen.
        if (!ReferenceEquals(_runtime, runtime))
        {
            CancelAnalysisIfPresent();
            _runtime?.Dispose();
        }
        _runtime = runtime;

        PipelineConfig = runtime.PipelineConfig;
        ModelName = runtime.ModelName;
        LiveDetection = runtime.LiveDetection;
        EnhancedVision = runtime.EnhancedVision;
        ProtocolVerifier = runtime.ProtocolVerifier;
        QualityGate = runtime.QualityGate;
        VisionClient = runtime.VisionClient;
        MultiModel = runtime.MultiModel;
        BoxSegmentation = runtime.BoxSegmentation;
        UseMultiModel = false;
        AiEnabled = runtime.MultiModelAvailable;
    }

    public bool TryBeginAnalysis()
    {
        if (!CanAnalyze || IsAnalyzing)
            return false;

        IsAnalyzing = true;
        _analysisCancellation = CancellationTokenSourceLifecycle.CancelPreviousAndCreate(_analysisCancellation);
        return true;
    }

    public void EndAnalysis()
        => IsAnalyzing = false;

    public void CancelAnalysisIfPresent()
        => CancellationTokenSourceLifecycle.CancelIfPresent(_analysisCancellation);

    public void DisposeAnalysisCancellation()
        => _analysisCancellation = CancellationTokenSourceLifecycle.CancelDisposeAndClear(_analysisCancellation);

    public void SetUseMultiModel(bool useMultiModel)
        => UseMultiModel = useMultiModel;

    public void SetAiEnabled(bool aiEnabled)
        => AiEnabled = aiEnabled;

    public void StartHealthMonitor(
        IPipelineHealthMonitor monitor,
        EventHandler<PipelineHealthStatus> statusChanged)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(statusChanged);

        _healthMonitor = monitor;
        _healthStatusChanged = statusChanged;
        _healthMonitor.StatusChanged += statusChanged;
        _healthMonitor.Start();
    }

    public Task<PipelineHealthStatus> RefreshHealthOnceAsync(CancellationToken ct = default)
    {
        if (_healthMonitor is null)
            throw new InvalidOperationException("Pipeline health monitor has not been started.");

        return _healthMonitor.RefreshOnceAsync(ct);
    }

    public Task? StopHealthMonitor()
    {
        AiEnabled = false;
        if (_healthMonitor is null)
            return null;

        if (_healthStatusChanged is not null)
            _healthMonitor.StatusChanged -= _healthStatusChanged;

        var stopTask = _healthMonitor.StopAsync();
        _healthMonitor = null;
        _healthStatusChanged = null;
        return stopTask;
    }

    public SingleFrameMultiModelService? EnsureMultiModel(Func<IVisionPipelineClient, PipelineConfig?, SingleFrameMultiModelService> create)
    {
        ArgumentNullException.ThrowIfNull(create);

        if (MultiModel is null && VisionClient is not null)
            MultiModel = create(VisionClient, PipelineConfig);

        return MultiModel;
    }

    // Gibt die aktuelle Runtime (eigener VisionClient/OllamaClient) und die Analyse-
    // Cancellation frei. Der PipelineHealthMonitor wird zuvor ueber StopHealthMonitor beendet.
    public void Dispose()
    {
        DisposeAnalysisCancellation();
        _runtime?.Dispose();
        _runtime = null;
    }
}
