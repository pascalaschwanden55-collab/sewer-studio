using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingProtocolMatchController
{
    CodingImportEventSeekCommandResult SeekSelectedImportEvent();
    CodingImportEventSeekCommandResult SeekImportEvent(CodingEvent importEvent);
    CodingProtocolMatchCommandResult RunMatch();
    void UpdateSummary(CodingMatchRouting? routing);
}

public sealed record CodingProtocolMatchControllerBindings(
    Func<object?> ResolveSelectedImportEvent,
    Func<bool> HasCodingSessionService,
    Action<long> SeekMilliseconds,
    Action<double> MoveToMeter,
    Action MarkNavigationPending,
    Action SyncVideoToCodingMeter,
    Func<bool> HasCodingViewModel,
    Func<CodingMatchRouting> RunMatch,
    Action<CodingMatchRouting> StoreMatch,
    Action<CodingMatchRouting?> ApplySummary,
    Action RefreshEvents,
    Action ScheduleHighlights);

public sealed class CodingProtocolMatchController : ICodingProtocolMatchController
{
    private readonly CodingProtocolMatchControllerBindings _bindings;

    public CodingProtocolMatchController(CodingProtocolMatchControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(bindings.ResolveSelectedImportEvent);
        ArgumentNullException.ThrowIfNull(bindings.HasCodingSessionService);
        ArgumentNullException.ThrowIfNull(bindings.SeekMilliseconds);
        ArgumentNullException.ThrowIfNull(bindings.MoveToMeter);
        ArgumentNullException.ThrowIfNull(bindings.MarkNavigationPending);
        ArgumentNullException.ThrowIfNull(bindings.SyncVideoToCodingMeter);
        ArgumentNullException.ThrowIfNull(bindings.HasCodingViewModel);
        ArgumentNullException.ThrowIfNull(bindings.RunMatch);
        ArgumentNullException.ThrowIfNull(bindings.StoreMatch);
        ArgumentNullException.ThrowIfNull(bindings.ApplySummary);
        ArgumentNullException.ThrowIfNull(bindings.RefreshEvents);
        ArgumentNullException.ThrowIfNull(bindings.ScheduleHighlights);

        _bindings = bindings;
    }

    public CodingImportEventSeekCommandResult SeekSelectedImportEvent()
        => SeekImportEvent(_bindings.ResolveSelectedImportEvent());

    public CodingImportEventSeekCommandResult SeekImportEvent(CodingEvent importEvent)
    {
        ArgumentNullException.ThrowIfNull(importEvent);
        return SeekImportEvent((object?)importEvent);
    }

    public CodingProtocolMatchCommandResult RunMatch()
        => CodingProtocolMatchCommandWorkflow.Execute(
            new CodingProtocolMatchCommandRequest(_bindings.HasCodingViewModel()),
            new CodingProtocolMatchCommandActions(
                RunMatch: _bindings.RunMatch,
                StoreMatch: _bindings.StoreMatch,
                UpdateSummary: UpdateSummary,
                RefreshEvents: _bindings.RefreshEvents,
                ScheduleHighlights: _bindings.ScheduleHighlights));

    public void UpdateSummary(CodingMatchRouting? routing)
        => _bindings.ApplySummary(routing);

    private CodingImportEventSeekCommandResult SeekImportEvent(object? selectedItem)
        => CodingImportEventSeekCommandWorkflow.Execute(
            new CodingImportEventSeekCommandRequest(
                selectedItem,
                HasCodingSessionService: _bindings.HasCodingSessionService()),
            new CodingImportEventSeekCommandActions(
                SeekMilliseconds: _bindings.SeekMilliseconds,
                MoveToMeter: _bindings.MoveToMeter,
                MarkNavigationPending: _bindings.MarkNavigationPending,
                SyncVideoToCodingMeter: _bindings.SyncVideoToCodingMeter));
}
