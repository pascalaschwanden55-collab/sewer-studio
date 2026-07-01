using System;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Stellt sicher, dass BCD (Rohranfang) als erster Eintrag existiert.
    /// Meter und Timestamp werden automatisch aus OSD / Video entnommen.
    /// </summary>
    private async Task<bool> EnsureRohranfangExistsAsync(
        double currentMeter,
        TimeSpan currentVideoTime,
        byte[]? analyzedFrameBytes)
    {
        var result = await CodingBoundaryEventCommandWorkflow.EnsureStartAsync(
            new CodingBoundaryStartCommandRequest(
                CurrentMeter: currentMeter,
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                ViewEvents: _codingSessionHost.EventCollection,
                SessionEvents: _codingSessionRuntimeOwner.Service?.ActiveSession?.Events ?? [],
                ImportEvents: _codingImportReferenceEvents.Events,
                CodingSessionService: _codingSessionRuntimeOwner.Service,
                FirstCleanFrameSeconds: _codingFrameReadinessController.FirstCleanFrameSeconds,
                AnalyzedFrameBytes: analyzedFrameBytes),
            new CodingBoundaryStartCommandActions(
                request => CodingBoundaryEventWorkflow.EnsureStartAsync(
                    request,
                    BoundaryEventWorkflowActions())));
        return result.Added;
    }

    /// <summary>
    /// Fuegt BCE (Rohrende) als letzten Eintrag ein.
    /// Meter und Timestamp werden automatisch aus OSD / Video entnommen.
    /// Aufgerufen beim Beenden der Codier-Session oder am Videoende.
    /// </summary>
    private void EnsureRohrendeExists(double meterEnd, TimeSpan videoTime, byte[]? analyzedFrameBytes = null)
    {
        CodingBoundaryEventCommandWorkflow.EnsureEnd(
            new CodingBoundaryEndCommandRequest(
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                ViewEvents: _codingSessionHost.EventCollection,
                ImportEvents: _codingImportReferenceEvents.Events,
                CodingSessionService: _codingSessionRuntimeOwner.Service,
                OsdMeter: _codingOsdMeterController.LastMeter,
                FallbackEndMeter: meterEnd,
                ViewModelEndMeter: _codingSessionHost.EndMeter,
                FallbackVideoTime: _playerTimelineHost.CurrentTimeOrZero,
                AnalyzedFrameBytes: analyzedFrameBytes),
            new CodingBoundaryEndCommandActions(
                request => CodingBoundaryEventWorkflow.EnsureEnd(
                    request,
                    BoundaryEventWorkflowActions())));
    }

    private CodingBoundaryEventWorkflowActions BoundaryEventWorkflowActions()
        => new(
            VsaCodeResolver.LookupLabel,
            message => PlayerTrace.WriteLine(message),
            TryExtractFrameAtSecondsAsync,
            (entry, frameBytes) => AttachBoundaryAnalyzedFramePhoto(entry, frameBytes),
            () => TryAutoCalibrationFromCurrentFrame().SafeFireAndForget("TryAutoCalibration"),
            RefreshCodingEventsList);
}
