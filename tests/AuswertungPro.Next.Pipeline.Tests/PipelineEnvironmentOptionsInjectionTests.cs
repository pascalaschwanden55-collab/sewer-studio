using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class PipelineEnvironmentOptionsInjectionTests
{
    [Fact]
    public void MultiModelService_uses_injected_classifier_options()
    {
        var options = new StubOptions(
            classifierDecision: true,
            classifierOnlyStructural: false,
            expectedYoloModel: "custom-yolo");
        var service = new MultiModelAnalysisService(
            new VisionPipelineClient(new Uri("http://127.0.0.1:8100")),
            CreateConfig(),
            pipelineEnvironmentOptions: options);

        Assert.True(service.ClassifierDecisionEnabled);
        Assert.False(service.ClassifierOnlyStructuralEnabled);
        Assert.Equal("custom-yolo", GetField<string>(service, "_expectedYoloModel"));
    }

    [Fact]
    public void SingleFrameService_uses_injected_thresholds()
    {
        var options = new StubOptions(
            yoloConfidence: 0.41,
            dinoBoxThreshold: 0.42,
            dinoTextThreshold: 0.43);
        var service = new SingleFrameMultiModelService(
            new VisionPipelineClient(new Uri("http://127.0.0.1:8100")),
            pipelineEnvironmentOptions: options);

        Assert.Equal(0.41, GetField<double>(service, "_yoloConfidence"));
        Assert.Equal(0.42, GetField<double>(service, "_dinoBoxThreshold"));
        Assert.Equal(0.43, GetField<double>(service, "_dinoTextThreshold"));
    }

    private static PipelineConfig CreateConfig()
        => new(
            MultiModelEnabled: true,
            SidecarUrl: new Uri("http://127.0.0.1:8100"),
            SidecarToken: null,
            Mode: PipelineMode.Auto,
            YoloConfidence: 0.25,
            YoloClassConfidence: new Dictionary<string, double>(),
            DinoBoxThreshold: 0.25,
            DinoTextThreshold: 0.20,
            SidecarTimeoutSec: 30,
            PipeDiameterMmOverride: null);

    private static T GetField<T>(object target, string name)
        => Assert.IsAssignableFrom<T>(target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(target));

    private sealed class StubOptions(
        bool classifierDecision = false,
        bool classifierOnlyStructural = true,
        string expectedYoloModel = PipelineEnvironmentOptions.DefaultExpectedYoloModel,
        double yoloConfidence = 0.25,
        double dinoBoxThreshold = 0.25,
        double dinoTextThreshold = 0.20) : IPipelineEnvironmentOptions
    {
        public bool ClassifierDecisionEnabled() => classifierDecision;

        public bool ClassifierOnlyStructuralEnabled() => classifierOnlyStructural;

        public string ExpectedYoloModel() => expectedYoloModel;

        public double? ReadDoubleWithCompat(string sewerStudioName)
            => sewerStudioName switch
            {
                PipelineEnvironmentOptions.YoloConfidenceEnvVar => yoloConfidence,
                PipelineEnvironmentOptions.DinoBoxThresholdEnvVar => dinoBoxThreshold,
                PipelineEnvironmentOptions.DinoTextThresholdEnvVar => dinoTextThreshold,
                _ => null,
            };

        public double ResolveDoubleWithCompat(string sewerStudioName, double defaultValue)
            => ReadDoubleWithCompat(sewerStudioName) ?? defaultValue;
    }
}
