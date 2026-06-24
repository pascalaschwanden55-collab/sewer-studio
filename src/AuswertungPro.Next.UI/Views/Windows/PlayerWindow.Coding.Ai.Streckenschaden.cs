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
    /// StreckenschadenActionMapper); hier wird nur gefiltert, der Tracker gefuettert und der Applier aufgerufen.
    ///
    /// Streckenschaden-Befunde (Code mit IsStreckenschadenCode) werden NICHT als Punkt-Events
    /// gefuehrt - die hier "verbrauchten" Segmente werden zurueckgegeben, damit der normale
    /// Punkt-Loop sie ueberspringt (referenzgleich, exakt die Streckenschaden-Codes).
    /// </summary>
    private HashSet<SegmentedFinding> ApplyStreckenschadenTracking(
        IReadOnlyList<SegmentedFinding> segmented, double meter, TimeSpan videoTime)
    {
        var codingSessionService = _codingSessionService;
        if (codingSessionService == null || !_codingSessionHost.HasViewModel)
            return [];

        // 1) Codierbare Streckenschaden-Befunde sammeln und Code aufloesen (gleicher Resolver wie Loop).
        var trackingInput = CodingStreckenschadenObservationBuilder.Build(
            segmented,
            meter,
            ResolveFindingCodeForCoding);

        // 2) Tracker fuettern (auch mit leerer Liste -> ermoeglicht Auto-Schliessen nach Toleranzdistanz).
        var actions = _streckenTracker.Update(trackingInput.Observations, meter);

        // 3) Aktionen in konkrete Anweisungen uebersetzen und ausfuehren.
        if (TryApplyStreckenschadenActions(actions, videoTime))
            RefreshCodingEventsList();
        return trackingInput.ConsumedSegments;
    }

    private bool TryApplyStreckenschadenActions(
        IReadOnlyList<StreckenschadenTracker.SegmentAction> actions,
        TimeSpan videoTime)
    {
        var codingSessionService = _codingSessionService;
        var codingEvents = _codingSessionHost.EventCollection;
        if (codingSessionService == null || codingEvents == null || actions.Count == 0)
            return false;

        return CodingStreckenschadenActionApplier.Apply(
            actions,
            codingEvents,
            codingSessionService,
            videoTime,
            LookupVsaLabel,
            entry => AttachAnalyzedFramePhoto(entry));
    }

    /// <summary>
    /// Schliesst ALLE vom Tracker gefuehrten offenen Strecken am angegebenen Meter (Pflicht bei
    /// Rohrende BCE / Abbruch BDC / Exit). Fuehrt die Close-Anweisungen aus; der bestehende
    /// CloseOpenStreckenschaeden-Dialog bleibt nur als Sicherheitsnetz fuer Reste.
    /// </summary>
    private void CloseTrackedStreckenschaeden(double endMeter)
    {
        var actions = _streckenTracker.CloseAll(endMeter);
        if (actions.Count == 0) return;
        var videoTime = _player != null ? TimeSpan.FromMilliseconds(_player.Time) : TimeSpan.Zero;
        if (TryApplyStreckenschadenActions(actions, videoTime))
            RefreshCodingEventsList();
    }
}
