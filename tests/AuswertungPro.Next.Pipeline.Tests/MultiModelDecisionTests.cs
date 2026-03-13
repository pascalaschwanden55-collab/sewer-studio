using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public class MultiModelDecisionTests
{
    /// <summary>
    /// Simuliert die Entscheidungslogik aus ShouldUseMultiModelAsync()
    /// vor dem Sidecar-Health-Check.
    /// </summary>
    private static bool ShouldAttemptSidecar(bool multiModelEnabled, PipelineMode mode)
    {
        if (mode == PipelineMode.OllamaOnly) return false;
        if (!multiModelEnabled && mode != PipelineMode.MultiModel) return false;
        return true; // würde zum Sidecar-Health-Check weitergehen
    }

    [Theory]
    [InlineData(false, PipelineMode.Auto,       false)]  // Kill-Switch greift
    [InlineData(false, PipelineMode.OllamaOnly, false)]  // OllamaOnly dominiert
    [InlineData(false, PipelineMode.MultiModel, true)]   // Expliziter Override
    [InlineData(true,  PipelineMode.Auto,       true)]   // Enabled + Auto → Sidecar versuchen
    [InlineData(true,  PipelineMode.OllamaOnly, false)]  // OllamaOnly dominiert
    [InlineData(true,  PipelineMode.MultiModel, true)]   // Enabled + Force → Sidecar versuchen
    public void MultiModelEnabled_KillSwitch_WorksCorrectly(
        bool enabled, PipelineMode mode, bool expectedAttempt)
    {
        Assert.Equal(expectedAttempt, ShouldAttemptSidecar(enabled, mode));
    }
}
