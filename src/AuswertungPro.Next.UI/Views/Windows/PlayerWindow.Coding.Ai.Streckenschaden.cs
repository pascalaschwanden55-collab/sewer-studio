using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Automatische Streckenschaden-Verfolgung (VSA 2.1.2). Laeuft bei JEDEM Analyse-Tick,
    /// auch mit leerer Streckenschaden-Liste - sonst koennte der Tracker offene Strecken nie
    /// automatisch schliessen. Die Fachregel liegt in Application (StreckenschadenTracker +
    /// StreckenschadenActionMapper); hier wird nur der Command-Workflow mit Fenster-Delegates verdrahtet.
    ///
    /// Streckenschaden-Befunde (Code mit IsStreckenschadenCode) werden NICHT als Punkt-Events
    /// gefuehrt - die hier "verbrauchten" Segmente werden zurueckgegeben, damit der normale
    /// Punkt-Loop sie ueberspringt (referenzgleich, exakt die Streckenschaden-Codes).
    /// </summary>
    private HashSet<SegmentedFinding> ApplyStreckenschadenTracking(
        IReadOnlyList<SegmentedFinding> segmented, double meter, TimeSpan videoTime)
    {
        var result = CodingStreckenschadenTrackingCommandWorkflow.ApplyTracking(
            new CodingStreckenschadenTrackingCommandRequest(
                Segmented: segmented,
                Meter: meter,
                VideoTime: videoTime,
                HasCodingSessionService: _codingSessionRuntimeOwner.Service is not null,
                HasCodingViewModel: _codingSessionHost.HasViewModel),
            new CodingStreckenschadenTrackingCommandActions(
                BuildObservations: (items, currentMeter) => CodingStreckenschadenObservationBuilder.Build(
                    items,
                    currentMeter,
                    ResolveFindingCodeForCoding),
                UpdateTracker: _streckenschadenTracker.Update,
                ApplyActions: TryApplyStreckenschadenActions,
                RefreshEvents: RefreshCodingEventsList));
        return result.ConsumedSegments;
    }

    private bool TryApplyStreckenschadenActions(
        IReadOnlyList<StreckenschadenTracker.SegmentAction> actions,
        TimeSpan videoTime)
    {
        var codingSessionService = _codingSessionRuntimeOwner.Service;
        var codingEvents = _codingSessionHost.EventCollection;

        return CodingStreckenschadenActionApplyCommandWorkflow.Execute(
            new CodingStreckenschadenActionApplyCommandRequest(
                HasCodingSessionService: codingSessionService is not null,
                HasCodingEvents: codingEvents is not null,
                HasActions: actions.Count > 0),
            new CodingStreckenschadenActionApplyCommandActions(
                ApplyActions: () => CodingStreckenschadenActionApplier.Apply(
                    actions,
                    codingEvents!,
                    codingSessionService!,
                    videoTime,
                    LookupVsaLabel,
                    entry => AttachAnalyzedFramePhoto(entry))))
            .Changed;
    }

    /// <summary>
    /// Schliesst ALLE vom Tracker gefuehrten offenen Strecken am angegebenen Meter (Pflicht bei
    /// Rohrende BCE / Abbruch BDC / Exit). Fuehrt die Close-Anweisungen aus; der bestehende
    /// CloseOpenStreckenschaeden-Dialog bleibt nur als Sicherheitsnetz fuer Reste.
    /// </summary>
    private void CloseTrackedStreckenschaeden(double endMeter)
    {
        CodingStreckenschadenTrackingCommandWorkflow.CloseTracked(
            new CodingStreckenschadenCloseTrackedCommandRequest(
                EndMeter: endMeter,
                VideoTime: _playerTimelineHost.CurrentTimeOrZero),
            new CodingStreckenschadenCloseTrackedCommandActions(
                CloseAll: _streckenschadenTracker.CloseAll,
                ApplyActions: TryApplyStreckenschadenActions,
                RefreshEvents: RefreshCodingEventsList));
    }
}
