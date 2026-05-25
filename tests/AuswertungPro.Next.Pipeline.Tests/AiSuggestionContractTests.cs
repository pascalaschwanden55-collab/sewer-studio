using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Monitoring;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class AiSuggestionContractTests
{
    [Fact]
    public void AiSuggestionDto_LivesInApplicationLayer_AndMapsToDomainResult()
    {
        var dto = new AiSuggestionResultDto
        {
            suggestedCode = "BAA",
            confidence = 0.82,
            rationale = "Riss sichtbar",
            evidence = "Frame 12",
            warnings = new[] { "manual-check" }
        };

        var result = dto.ToDomain();

        Assert.Equal("BAA", result.SuggestedCode);
        Assert.Equal(0.82, result.Confidence);
        Assert.Equal("Riss sichtbar", result.Rationale);
        Assert.Equal("Frame 12", result.Evidence);
        Assert.Equal(new[] { "manual-check" }, result.Warnings);
    }

    [Fact]
    public void AiSuggestionSchema_IsOwnedByApplicationLayer()
    {
        var schema = AiSuggestionSchemas.AiSuggestionResultSchema;

        Assert.Equal(JsonValueKind.Object, schema.ValueKind);
        Assert.True(schema.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("suggestedCode", out _));
        Assert.True(properties.TryGetProperty("confidence", out _));
    }

    [Fact]
    public void ProtocolAiContracts_LiveInApplicationLayer()
    {
        var input = new AiInput(
            ProjectFolderAbs: @"C:\Projekt",
            HaltungId: "1.001-1.002",
            Meter: 12.3,
            ExistingCode: "BAA",
            ExistingText: "Riss",
            AllowedCodes: new[] { "BAA" });

        var suggestion = new AiSuggestion("BAA", 0.8, "Plausibel", new[] { "test" });

        Assert.Equal("1.001-1.002", input.HaltungId);
        Assert.Equal("BAA", suggestion.SuggestedCode);
        Assert.StartsWith("AuswertungPro.Next.Application", typeof(IProtocolAiService).Namespace!, StringComparison.Ordinal);
    }

    [Fact]
    public void PipelineConfig_LivesInApplicationLayer()
    {
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(PipelineConfig).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(PipelineMode).Namespace!,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSettings_LiveOutsideUiLayer()
    {
        Assert.StartsWith("AuswertungPro.Next.Application", typeof(AiRuntimeSettings).Namespace);
        AssertNoUiType("Ai" + "RuntimeConfig");
    }

    [Fact]
    public void PlatformSettings_LiveOutsideUiLayer()
    {
        Assert.StartsWith("AuswertungPro.Next.Application", typeof(AiPlatformSettings).Namespace);
        AssertNoUiType("Ai" + "PlatformConfig");
    }

    [Fact]
    public void GroundTruthTrainingModels_LiveInApplicationLayer()
    {
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(GroundTruthEntry).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(QuantificationDetail).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(SelfTrainingResult).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(ComparisonResult).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(MatchLevel).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(GroundTruthProtocolEntryMapper).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(TrainingCenterSettings).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(SelfTrainingRunSnapshot).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(TrainingSample).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(TrainingSampleStatus).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(KbIndexState).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(MatchLevelNames).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(SourceTypeNames).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(CodingEventToSampleMapper).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(LiveDetection).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(LiveFrameFinding).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(EnhancedFrameAnalysis).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(EnhancedFinding).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(TeacherAnnotation).Namespace!,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SanierungCore_LivesOutsideUiLayer()
    {
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(AiOptimizationSession).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(CostOptimizationEngine).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(SanierungValidationService).Namespace!,
            StringComparison.Ordinal);
        AssertSimpleTypeNamespace("AiOptimizationSessionStore", "AuswertungPro.Next.Infrastructure");
        AssertSimpleTypeNamespace("AiSanierungOptimizationService", "AuswertungPro.Next.Infrastructure");
    }

    [Fact]
    public void MonitoringServices_LiveInInfrastructureLayer()
    {
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(AccuracyDashboardService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(ConfidenceDistributionTracker).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(ModelRegistryService).Namespace!,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ToolAndGpuDetection_LiveInInfrastructureLayer()
    {
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(FfmpegLocator).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(GpuModelSelector).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(OverlayToolService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(SchemaOverlayManager).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(PipeBendSchema).Namespace!,
            StringComparison.Ordinal);
    }

    [Fact]
    public void KnowledgePaths_LiveOutsideUiLayer()
    {
        AssertSimpleTypeNamespace("KnowledgeBasePaths", "AuswertungPro.Next.Infrastructure");
        AssertSimpleTypeNamespace("KnowledgeBaseManager", "AuswertungPro.Next.Infrastructure");
        AssertNoUiType("KnowledgeRoot");
    }

    [Fact]
    public void VsaCatalogAndResolver_LiveOutsideUiLayer()
    {
        AssertSimpleTypeNamespace("VsaCodeTree", "AuswertungPro.Next.Domain");
        AssertSimpleTypeNamespace("VsaCodeResolver", "AuswertungPro.Next.Infrastructure");
    }

    [Fact]
    public void TrainingIoServices_LiveInInfrastructureLayer()
    {
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(PdfProtocolExtractor).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(SceneChangeDetector).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(VideoProbeService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(GuidedVerificationService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(GuidedVerificationResult).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(SelfTrainingComparisonService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(YoloDatasetExportService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(YoloExportResult).Namespace!,
            StringComparison.Ordinal);
        AssertSimpleTypeNamespace("MeterTimelineService", "AuswertungPro.Next.Infrastructure");
    }

    [Fact]
    public void TrainingStores_LiveInInfrastructureLayer()
    {
        AssertSimpleTypeNamespace("FrameStore", "AuswertungPro.Next.Infrastructure");
        AssertSimpleTypeNamespace("TrainingSamplesStore", "AuswertungPro.Next.Infrastructure");
        AssertSimpleTypeNamespace("TrainingCenterSettingsStore", "AuswertungPro.Next.Infrastructure");
        AssertSimpleTypeNamespace("SelfTrainingHistoryStore", "AuswertungPro.Next.Infrastructure");
        AssertSimpleTypeNamespace("FewShotExampleStore", "AuswertungPro.Next.Infrastructure");
        AssertSimpleTypeNamespace("FewShotExample", "AuswertungPro.Next.Infrastructure");
        AssertSimpleTypeNamespace("FewShotExampleBuilder", "AuswertungPro.Next.Infrastructure");
        AssertSimpleTypeNamespace("FewShotBuildProgress", "AuswertungPro.Next.Infrastructure");
        AssertSimpleTypeNamespace("FewShotBuildResult", "AuswertungPro.Next.Infrastructure");
    }

    [Fact]
    public void TeacherStores_LiveInInfrastructureLayer()
    {
        AssertSimpleTypeNamespace("TeacherAnnotationStore", "AuswertungPro.Next.Infrastructure");
        AssertSimpleTypeNamespace("VsaYoloClassMap", "AuswertungPro.Next.Infrastructure");
    }

    [Fact]
    public void VideoProcessingHelpers_LiveInInfrastructureLayer()
    {
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(QuickScanService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(QuickScanSegment).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(BoundaryPhotoService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(LiveDetectionService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(EnhancedVisionAnalysisService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(VideoFullAnalysisService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(MultiModelAnalysisService).Namespace!,
            StringComparison.Ordinal);
        AssertSimpleTypeNamespace("LiveDetectionMapper", "AuswertungPro.Next.Infrastructure");
    }

    [Fact]
    public void SelfImprovingStorageAnalysis_LivesInInfrastructureLayer()
    {
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(KbQualityService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(StaleSampleCandidate).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(AutoApprovalService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(AutoApprovalResult).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(ReviewQueueService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(ReviewQueueItem).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(ActiveLearningSelector).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(CodingFeedbackRecorder).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(FeedbackIngestionService).Namespace!,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OllamaFrameAndMeterHelpers_LiveInInfrastructureLayer()
    {
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(OllamaVisionFindingsService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(OsdMeterDetectionService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(VideoFrameExtractor).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(VideoFrameStream).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(OllamaProtocolAiService).Namespace!,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VisionPipelineClient_LivesInInfrastructureLayer()
    {
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(VisionPipelineClient).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(YoloRequest).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(SamResponse).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(MaskQuantificationService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(PipelineTelemetry).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(SingleFrameMultiModelService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(SingleFrameResult).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(TrainingExportService).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Infrastructure",
            typeof(TrainingExportResult).Namespace!,
            StringComparison.Ordinal);
    }

    [Fact]
    public void QualityGateDataModels_LiveInApplicationLayer()
    {
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(EvidenceVector).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(QualityGateResult).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(TrafficLight).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(UncertaintyEstimate).Namespace!,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "AuswertungPro.Next.Application",
            typeof(UncertaintySource).Namespace!,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VideoPipelineContracts_LiveInApplicationLayer()
    {
        var expected = "AuswertungPro.Next.Application";

        Assert.StartsWith(expected, typeof(IVideoAnalysisPipelineService).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(PipelineRequest).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(PipelineResult).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(PipelineStats).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(PipelineProgress).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(PipelinePhase).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(VideoAnalysisResult).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(VideoAnalysisProgress).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(RawVideoDetection).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(FullProtocolGenerationRequest).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(FullProtocolGenerationResult).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(MappedProtocolEntry).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(CodeMappingProgress).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(FrameTiming).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(TelemetrySummary).Namespace!, StringComparison.Ordinal);
        Assert.StartsWith(expected, typeof(PhaseStat).Namespace!, StringComparison.Ordinal);
    }

    private static void AssertSimpleTypeNamespace(string typeName, string expectedNamespacePrefix)
    {
        var assemblies = new[]
        {
            typeof(AiSuggestionResultDto).Assembly,
            typeof(OllamaClient).Assembly,
            typeof(HaltungRecord).Assembly,
            typeof(SamMaskRenderer).Assembly
        };

        var matches = assemblies
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(type => string.Equals(type.Name, typeName, StringComparison.Ordinal))
            .Select(type => type.Namespace ?? "")
            .ToArray();

        Assert.Contains(matches, ns => ns.StartsWith(expectedNamespacePrefix, StringComparison.Ordinal));
        Assert.DoesNotContain(matches, ns => ns.StartsWith("AuswertungPro.Next.UI", StringComparison.Ordinal));
    }

    private static void AssertNoUiType(string typeName)
    {
        var assemblies = new[]
        {
            typeof(AiSuggestionResultDto).Assembly,
            typeof(OllamaClient).Assembly,
            typeof(HaltungRecord).Assembly,
            typeof(SamMaskRenderer).Assembly
        };

        var matches = assemblies
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(type => string.Equals(type.Name, typeName, StringComparison.Ordinal))
            .Select(type => type.Namespace ?? "")
            .ToArray();

        Assert.DoesNotContain(matches, ns => ns.StartsWith("AuswertungPro.Next.UI", StringComparison.Ordinal));
    }
}
