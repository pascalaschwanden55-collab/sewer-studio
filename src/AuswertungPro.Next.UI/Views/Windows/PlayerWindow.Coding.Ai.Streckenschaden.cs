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
    /// StreckenschadenActionMapper); hier wird nur gefiltert, aufgerufen und Events angelegt/geaendert.
    ///
    /// Streckenschaden-Befunde (Code mit IsStreckenschadenCode) werden NICHT als Punkt-Events
    /// gefuehrt - die hier "verbrauchten" Segmente werden zurueckgegeben, damit der normale
    /// Punkt-Loop sie ueberspringt (referenzgleich, exakt die Streckenschaden-Codes).
    /// </summary>
    private HashSet<SegmentedFinding> ApplyStreckenschadenTracking(
        IReadOnlyList<SegmentedFinding> segmented, double meter, TimeSpan videoTime)
    {
        var codingSessionService = _codingSessionService;
        var codingVm = _codingVm;
        if (codingSessionService == null || codingVm == null)
            return [];

        // 1) Codierbare Streckenschaden-Befunde sammeln und Code aufloesen (gleicher Resolver wie Loop).
        var trackingInput = CodingStreckenschadenObservationBuilder.Build(
            segmented,
            meter,
            ResolveFindingCodeForCoding);

        // 2) Tracker fuettern (auch mit leerer Liste -> ermoeglicht Auto-Schliessen nach Toleranzdistanz).
        var actions = _streckenTracker.Update(trackingInput.Observations, meter);

        // 3) Aktionen in konkrete Anweisungen uebersetzen und ausfuehren.
        ApplyStreckenschadenActions(actions, videoTime);
        return trackingInput.ConsumedSegments;
    }

    /// <summary>
    /// Fuehrt die vom Mapper bestimmten Anweisungen aus: offenen Streckenschaden-Eintrag anlegen
    /// bzw. einen bestehenden schliessen (MeterEnd setzen). Keine Fachlogik hier.
    /// </summary>
    private void ApplyStreckenschadenActions(
        IReadOnlyList<StreckenschadenTracker.SegmentAction> actions,
        TimeSpan videoTime)
    {
        var codingSessionService = _codingSessionService;
        var codingVm = _codingVm;
        if (codingSessionService == null || codingVm == null || actions.Count == 0)
            return;

        // Aktuell offene Streckenschaden-Eintraege als Mapper-Sicht (Referenz = CodingEvent).
        var openEntries = CodingStreckenschadenActionInputBuilder.BuildOpenEntries(codingVm.Events);

        var instructions = StreckenschadenActionMapper.MapAll(actions, openEntries);
        if (instructions.Count == 0) return;

        bool anyChanged = false;
        foreach (var instr in instructions)
        {
            switch (instr.Kind)
            {
                case StreckenschadenActionMapper.InstructionKind.CreateOpen:
                {
                    var draft = CodingStreckenschadenEventFactory.CreateOpen(
                        instr.MainCode,
                        LookupVsaLabel(instr.MainCode),
                        instr.StartMeter,
                        videoTime);
                    AttachAnalyzedFramePhoto(draft.Entry);
                    var ev = codingSessionService.AddEvent(draft.Entry);
                    ev.MeterAtCapture = instr.StartMeter;
                    ev.AiContext = draft.AiContext;
                    anyChanged = true;
                    break;
                }
                case StreckenschadenActionMapper.InstructionKind.CloseExisting:
                {
                    if (instr.TargetReference is CodingEvent target)
                    {
                        target.Entry.MeterEnd = instr.EndMeter;
                        target.Entry.IsStreckenschaden = true;
                        codingSessionService.UpdateEvent(target.EventId, target.Entry, target.Overlay);
                        anyChanged = true;
                    }
                    break;
                }
            }
        }

        if (anyChanged)
            RefreshCodingEventsList();
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
        ApplyStreckenschadenActions(actions, videoTime);
    }
}
