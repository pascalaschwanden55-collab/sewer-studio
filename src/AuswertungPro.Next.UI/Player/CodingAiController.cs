using System;
using System.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingAiController
{
    private CancellationTokenSource? _analysisCancellation;

    public LiveDetectionService? LiveDetection { get; private set; }
    public EnhancedVisionAnalysisService? EnhancedVision { get; private set; }
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
    public bool QwenAvailable => LiveDetection is not null || EnhancedVision is not null;
    public bool CanAnalyze => LiveDetection is not null || EnhancedVision is not null || MultiModel is not null;

    public void ApplyRuntime(CodingAiRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        PipelineConfig = runtime.PipelineConfig;
        ModelName = runtime.ModelName;
        LiveDetection = runtime.LiveDetection;
        EnhancedVision = runtime.EnhancedVision;
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

    public SingleFrameMultiModelService? EnsureMultiModel(Func<IVisionPipelineClient, PipelineConfig?, SingleFrameMultiModelService> create)
    {
        ArgumentNullException.ThrowIfNull(create);

        if (MultiModel is null && VisionClient is not null)
            MultiModel = create(VisionClient, PipelineConfig);

        return MultiModel;
    }
}
