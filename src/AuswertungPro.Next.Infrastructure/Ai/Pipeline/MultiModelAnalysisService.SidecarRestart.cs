using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Startup;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

// Paket 3/A2: Kontrollierter Sidecar-Neustart am Ausfall-Limit (einmalig pro Lauf).
// Ausgelagert in eine eigene Partial-Datei, damit die Hauptdatei unter dem
// 1000-Zeilen-Deckel bleibt (MaintainabilityFitnessTests).
public sealed partial class MultiModelAnalysisService
{
    // Kontrollierter Neustart-Dienst: null = heutiges Verhalten ohne Neustart
    // (Tests/aeltere Aufrufer) — am Ausfall-Limit wird sofort degraded abgebrochen.
    private readonly ISidecarRestartService? _sidecarRestart;

    // Neustart-Budget: genau EIN Versuch pro AnalyzeAsync-Lauf (wird dort zurueckgesetzt).
    private bool _sidecarRestartAttemptedThisRun;

    /// <summary>
    /// Zaehlt den Transportfehler des aktuellen Frames; versucht am Limit EINMAL pro Lauf
    /// einen kontrollierten Neustart (nur mit injiziertem Restart-Dienst). Bei Erfolg wird
    /// die Fehler-Serie zurueckgesetzt und der Lauf fortgesetzt (das Checkpoint-Journal
    /// begrenzt den Verlust); bei Misserfolg, abgelehntem Neustart (fremder Sidecar) oder
    /// einem zweiten Ausloeser wird wie bisher degraded abgebrochen.
    /// true = Lauf abbrechen (degraded).
    /// </summary>
    private async Task<bool> HandleSidecarTransportErrorAsync(
        SidecarOutageGuard outageGuard,
        Action markOutage,
        IProgress<VideoAnalysisProgress>? progress,
        int frameIndex,
        int totalFrames,
        CancellationToken ct)
    {
        outageGuard.RegisterTransportError(frameIndex);
        if (!outageGuard.LimitReached)
            return false;

        if (!_sidecarRestartAttemptedThisRun && _sidecarRestart is not null)
        {
            _sidecarRestartAttemptedThisRun = true;
            if (await TryRestartSidecarAsync(progress, frameIndex, totalFrames, ct).ConfigureAwait(false))
            {
                outageGuard.ResetSeries();
                return false;
            }
        }

        markOutage();
        _logger.LogError(
            "Sidecar antwortet seit {Count} Frames nicht — Analyse abgebrochen (degraded).",
            outageGuard.ConsecutiveErrorFrames);
        return true;
    }

    /// <summary>true = Neustart erfolgreich, der Lauf kann weiterlaufen.</summary>
    private async Task<bool> TryRestartSidecarAsync(
        IProgress<VideoAnalysisProgress>? progress,
        int frameIndex,
        int totalFrames,
        CancellationToken ct)
    {
        _logger.LogWarning("Sidecar-Ausfall: versuche kontrollierten Neustart (einmalig pro Lauf).");
        progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
            "Sidecar antwortet nicht – kontrollierter Neustart wird versucht (einmalig)..."));

        SidecarRestartResult result;
        try
        {
            result = await _sidecarRestart!.TryRestartAsync(
                    new Progress<string>(msg => progress?.Report(
                        new VideoAnalysisProgress(frameIndex, totalFrames, msg))),
                    ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ein scheiternder Neustart-Dienst darf den Lauf nicht mit einer Ausnahme
            // abreissen — er ist nur ein Erholungsversuch; danach gilt der bisherige Abbruch.
            result = new SidecarRestartResult(Attempted: true, Succeeded: false, Reason: ex.Message);
        }

        if (result is { Attempted: true, Succeeded: true })
        {
            _logger.LogInformation(
                "Sidecar-Neustart erfolgreich — Analyse laeuft weiter (Checkpoint-Journal begrenzt den Verlust).");
            progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                "Sidecar-Neustart erfolgreich – Analyse läuft weiter."));
            return true;
        }

        _logger.LogError(
            "Sidecar-Neustart {Outcome}: {Reason}",
            result.Attempted ? "fehlgeschlagen" : "abgelehnt (fremd gestarteter Sidecar – graceful Degraded)",
            result.Reason);
        progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
            result.Attempted
                ? "Sidecar-Neustart fehlgeschlagen – Analyse wird beendet (degraded)."
                : "Sidecar wurde nicht von der App gestartet – kein Neustart, Analyse wird beendet (degraded)."));
        return false;
    }
}
