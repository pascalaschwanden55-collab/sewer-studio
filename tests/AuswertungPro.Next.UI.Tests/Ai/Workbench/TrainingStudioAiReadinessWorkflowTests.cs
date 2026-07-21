using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingStudioAiReadinessWorkflowTests
{
    [Fact]
    public async Task Bereits_bereiter_Sidecar_wird_nicht_nochmals_gestartet()
    {
        var starts = 0;
        var workflow = CreateWorkflow(
            new PipelineHealthCheckResult(true, true, 200, Health: null, Error: null),
            () =>
            {
                starts++;
                return Startup(sidecarReachable: true);
            });

        var result = await workflow.EnsureReadyAsync(new Progress<string>(), CancellationToken.None);

        Assert.True(result.Ready);
        Assert.Equal(0, starts);
    }

    [Fact]
    public async Task Offline_Sidecar_wird_ueber_den_zentralen_KI_Start_gestartet()
    {
        var starts = 0;
        var workflow = CreateWorkflow(
            new PipelineHealthCheckResult(false, false, null, Health: null, Error: "offline"),
            () =>
            {
                starts++;
                return Startup(sidecarReachable: true);
            });

        var result = await workflow.EnsureReadyAsync(new Progress<string>(), CancellationToken.None);

        Assert.True(result.Ready);
        Assert.Equal(1, starts);
        Assert.Contains("bereit", result.StatusText);
    }

    [Fact]
    public async Task Falscher_Token_startet_keinen_zweiten_Sidecar()
    {
        var starts = 0;
        var workflow = CreateWorkflow(
            new PipelineHealthCheckResult(true, false, 401, Health: null, Error: "unauthorized"),
            () =>
            {
                starts++;
                return Startup(sidecarReachable: true);
            });

        var result = await workflow.EnsureReadyAsync(new Progress<string>(), CancellationToken.None);

        Assert.False(result.Ready);
        Assert.Equal(0, starts);
        Assert.Contains("Token", result.StatusText);
    }

    [Fact]
    public async Task Fehlgeschlagener_Start_liefert_eine_verstaendliche_Meldung()
    {
        var workflow = CreateWorkflow(
            new PipelineHealthCheckResult(false, false, null, Health: null, Error: "offline"),
            () => Startup(sidecarReachable: false));

        var result = await workflow.EnsureReadyAsync(new Progress<string>(), CancellationToken.None);

        Assert.False(result.Ready);
        Assert.Contains("nicht gestartet", result.StatusText);
    }

    private static TrainingStudioAiReadinessWorkflow CreateWorkflow(
        PipelineHealthCheckResult health,
        Func<AiStartupResult> start)
        => new(
            _ => Task.FromResult(health),
            (_, _) => Task.FromResult(start()));

    private static AiStartupResult Startup(bool sidecarReachable)
        => new(
            SettingsChanged: false,
            OllamaReachable: true,
            OllamaStartAttempted: false,
            OllamaStartSucceeded: false,
            SidecarReachable: sidecarReachable,
            SidecarStartAttempted: true,
            SidecarStartSucceeded: sidecarReachable,
            PreloadedModels: Array.Empty<string>(),
            Messages: Array.Empty<string>(),
            Warnings: Array.Empty<string>());
}
