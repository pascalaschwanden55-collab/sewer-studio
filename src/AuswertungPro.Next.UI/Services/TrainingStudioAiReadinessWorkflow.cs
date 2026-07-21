using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Startup;

namespace AuswertungPro.Next.UI.Services;

internal sealed record TrainingStudioAiReadinessResult(bool Ready, string StatusText);

/// <summary>
/// Prueft die Vision-KI des Testcenters und startet bei einem lokalen Ausfall den
/// vorhandenen zentralen KI-Startweg. Prozess- und Modelllogik bleiben im AiStartupService.
/// </summary>
internal sealed class TrainingStudioAiReadinessWorkflow
{
    private readonly Func<CancellationToken, Task<PipelineHealthCheckResult>> _checkHealth;
    private readonly Func<IProgress<string>, CancellationToken, Task<AiStartupResult>> _startAi;

    public TrainingStudioAiReadinessWorkflow(
        Func<CancellationToken, Task<PipelineHealthCheckResult>> checkHealth,
        Func<IProgress<string>, CancellationToken, Task<AiStartupResult>> startAi)
    {
        _checkHealth = checkHealth ?? throw new ArgumentNullException(nameof(checkHealth));
        _startAi = startAi ?? throw new ArgumentNullException(nameof(startAi));
    }

    public async Task<TrainingStudioAiReadinessResult> EnsureReadyAsync(
        IProgress<string> progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(progress);

        progress.Report("Pruefe lokale Vision-KI...");
        var health = await _checkHealth(ct).ConfigureAwait(false);
        if (health.IsReachable && health.IsAuthorized && health.Error is null)
            return new TrainingStudioAiReadinessResult(true, "Vision-KI bereit. Foto laden und Box ziehen.");

        if (health.IsReachable && !health.IsAuthorized)
        {
            return new TrainingStudioAiReadinessResult(
                false,
                "Vision-KI laeuft, aber die Anmeldung stimmt nicht. Bitte Sidecar-Token pruefen.");
        }

        if (health.IsReachable)
        {
            return new TrainingStudioAiReadinessResult(
                false,
                "Vision-KI antwortet, ist aber nicht einsatzbereit. Bitte KI erneut starten.");
        }

        progress.Report("Vision-KI ist aus. Lokale KI wird gestartet...");
        var startup = await _startAi(progress, ct).ConfigureAwait(false);
        if (!startup.SidecarReachable)
        {
            return new TrainingStudioAiReadinessResult(
                false,
                "Vision-KI konnte nicht gestartet werden. Bitte Startskript und Einstellungen pruefen.");
        }

        return new TrainingStudioAiReadinessResult(
            true,
            startup.HasWarnings
                ? "Vision-KI ist bereit. Beim restlichen KI-Start gab es eine Warnung."
                : "Vision-KI bereit. Foto laden und Box ziehen.");
    }
}
