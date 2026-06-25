using System;
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
    private void EnsureRohranfangExists(double currentMeter, TimeSpan currentVideoTime, byte[]? analyzedFrameBytes, ref bool anyAdded)
    {
        if (!_codingSessionHost.HasViewModel || _codingSessionRuntimeOwner.Service == null) return;

        var viewEvents = _codingSessionHost.EventCollection;
        if (viewEvents is null) return;

        var result = CodingBoundaryEventWorkflow.EnsureStart(
            new CodingBoundaryStartEventWorkflowRequest(
                currentMeter,
                viewEvents,
                _codingSessionRuntimeOwner.Service.ActiveSession?.Events ?? [],
                _codingImportEvents,
                _codingSessionRuntimeOwner.Service,
                _codingFrameReadinessController.FirstCleanFrameSeconds,
                analyzedFrameBytes),
            BoundaryEventWorkflowActions());
        anyAdded = result.Added;
    }

    /// <summary>
    /// Fuegt BCE (Rohrende) als letzten Eintrag ein.
    /// Meter und Timestamp werden automatisch aus OSD / Video entnommen.
    /// Aufgerufen beim Beenden der Codier-Session oder am Videoende.
    /// </summary>
    private void EnsureRohrendeExists(double meterEnd, TimeSpan videoTime, byte[]? analyzedFrameBytes = null)
    {
        if (!_codingSessionHost.HasViewModel || _codingSessionRuntimeOwner.Service == null) return;

        var viewEvents = _codingSessionHost.EventCollection;
        if (viewEvents is null) return;

        var fallbackEndTime = _player != null
            ? TimeSpan.FromMilliseconds(_player.Time)
            : videoTime;

        CodingBoundaryEventWorkflow.EnsureEnd(
            new CodingBoundaryEndEventWorkflowRequest(
                viewEvents,
                _codingImportEvents,
                _codingSessionRuntimeOwner.Service,
                _codingOsdMeterController.LastMeter,
                meterEnd,
                _codingSessionHost.EndMeter,
                fallbackEndTime,
                analyzedFrameBytes),
            BoundaryEventWorkflowActions());
    }

    private CodingBoundaryEventWorkflowActions BoundaryEventWorkflowActions()
        => new(
            VsaCodeResolver.LookupLabel,
            message => PlayerTrace.WriteLine(message),
            TryExtractFrameAtSeconds,
            (entry, frameBytes) => AttachBoundaryAnalyzedFramePhoto(entry, frameBytes),
            () => TryAutoCalibrationFromCurrentFrame().SafeFireAndForget("TryAutoCalibration"),
            RefreshCodingEventsList);
}
