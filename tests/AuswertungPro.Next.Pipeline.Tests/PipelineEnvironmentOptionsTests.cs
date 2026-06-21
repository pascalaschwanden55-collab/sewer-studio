using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection("EnvironmentVars")]
public sealed class PipelineEnvironmentOptionsTests
{
    [Fact]
    public void Classifier_flags_keep_existing_defaults()
    {
        using var scope = new PipelineEnvScope();

        Assert.False(PipelineEnvironmentOptions.ClassifierDecisionEnabled());
        Assert.True(PipelineEnvironmentOptions.ClassifierOnlyStructuralEnabled());
    }

    [Fact]
    public void Classifier_flags_read_canonical_env_values()
    {
        using var scope = new PipelineEnvScope(
            (PipelineEnvironmentOptions.ClassifierDecisionEnvVar, "1"),
            (PipelineEnvironmentOptions.ClassifierOnlyStructuralOffEnvVar, "true"));

        Assert.True(PipelineEnvironmentOptions.ClassifierDecisionEnabled());
        Assert.False(PipelineEnvironmentOptions.ClassifierOnlyStructuralEnabled());
    }

    [Fact]
    public void ExpectedYoloModel_uses_trimmed_override_or_default()
    {
        using (new PipelineEnvScope((PipelineEnvironmentOptions.ExpectedYoloModelEnvVar, "  yolo_custom.pt  ")))
        {
            Assert.Equal("yolo_custom.pt", PipelineEnvironmentOptions.ExpectedYoloModel());
        }

        using (new PipelineEnvScope((PipelineEnvironmentOptions.ExpectedYoloModelEnvVar, "  ")))
        {
            Assert.Equal("yolo26m", PipelineEnvironmentOptions.ExpectedYoloModel());
        }
    }

    [Fact]
    public void ReadDoubleWithCompat_prefers_canonical_over_legacy_name()
    {
        using var scope = new PipelineEnvScope(
            (PipelineEnvironmentOptions.YoloConfidenceEnvVar, "0.42"),
            ("AUSWERTUNGPRO_YOLO_CONFIDENCE", "0.11"));

        Assert.Equal(0.42, PipelineEnvironmentOptions.ReadDoubleWithCompat(PipelineEnvironmentOptions.YoloConfidenceEnvVar));
    }

    [Fact]
    public void ReadDoubleWithCompat_falls_back_to_legacy_name()
    {
        using var scope = new PipelineEnvScope(
            ("AUSWERTUNGPRO_DINO_BOX_THRESHOLD", "0.33"));

        Assert.Equal(0.33, PipelineEnvironmentOptions.ReadDoubleWithCompat(PipelineEnvironmentOptions.DinoBoxThresholdEnvVar));
    }

    [Fact]
    public void ResolveDoubleWithCompat_returns_default_for_missing_or_invalid_value()
    {
        using var scope = new PipelineEnvScope(
            (PipelineEnvironmentOptions.DinoTextThresholdEnvVar, "not-a-number"));

        Assert.Equal(0.20, PipelineEnvironmentOptions.ResolveDoubleWithCompat(
            PipelineEnvironmentOptions.DinoTextThresholdEnvVar,
            0.20));
    }

    private sealed class PipelineEnvScope : IDisposable
    {
        private static readonly string[] ManagedNames =
        [
            PipelineEnvironmentOptions.ClassifierDecisionEnvVar,
            PipelineEnvironmentOptions.ClassifierOnlyStructuralOffEnvVar,
            PipelineEnvironmentOptions.ExpectedYoloModelEnvVar,
            PipelineEnvironmentOptions.YoloConfidenceEnvVar,
            PipelineEnvironmentOptions.DinoBoxThresholdEnvVar,
            PipelineEnvironmentOptions.DinoTextThresholdEnvVar,
            "AUSWERTUNGPRO_YOLO_CONFIDENCE",
            "AUSWERTUNGPRO_DINO_BOX_THRESHOLD",
            "AUSWERTUNGPRO_DINO_TEXT_THRESHOLD"
        ];

        private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

        public PipelineEnvScope(params (string Name, string? Value)[] values)
        {
            foreach (var name in ManagedNames)
            {
                _previous[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }

            foreach (var (name, value) in values)
                Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previous)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
