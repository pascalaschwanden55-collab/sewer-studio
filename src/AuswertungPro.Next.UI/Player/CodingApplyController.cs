using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingApplyController
{
    bool Apply(bool showOverlay);
    bool ConfirmCanClose();
    void MarkProjectDirty();
}

public sealed record CodingApplyControllerBindings(
    Func<bool> HasCodingViewModel,
    Func<HaltungRecord?> GetHaltungRecord,
    Func<IReadOnlyList<CodingEvent>?> GetEventCollection,
    Action<ProtocolEntry> AddAutomaticBoundaryEvent,
    Func<IEnumerable<CodingEvent>> GetEvents,
    Func<bool> IsCodingMode,
    Func<string> GetBaselineSignature,
    Func<CodingApplyEmptyProtocolGuardResult, bool> ConfirmEmptyProtocol,
    Action<ProtocolDocument> AssignProtocol,
    Action<HaltungRecord?> MarkProjectDirty,
    Action<ProtocolDocument> SyncCodingToPrimaryDamages,
    Action<IReadOnlyList<CodingEvent>> PersistCodingEventsAsTrainingSamples,
    Action<string> SetBaselineSignature,
    Action SaveProjectAfterCoding,
    Action<string, TimeSpan> ShowOverlay,
    Func<Func<bool>, bool> ConfirmUnappliedChanges,
    Func<HaltungRecord?, double?> GetHaltungslaenge,
    Func<CodingApplyPipeEndPrompt, CodingApplyPipeEndDecision> ConfirmMissingPipeEnd);

public sealed class CodingApplyController : ICodingApplyController
{
    private readonly CodingApplyControllerBindings _bindings;

    public CodingApplyController(CodingApplyControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        _bindings = bindings;
    }

    public bool Apply(bool showOverlay)
    {
        var haltungRecord = _bindings.GetHaltungRecord();
        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                _bindings.HasCodingViewModel(),
                haltungRecord,
                _bindings.GetEventCollection(),
                showOverlay,
                _bindings.GetHaltungslaenge(haltungRecord)),
            new CodingApplyChangesWorkflowActions(
                ConfirmEmptyProtocol: _bindings.ConfirmEmptyProtocol,
                AddAutomaticBoundaryEvent: _bindings.AddAutomaticBoundaryEvent,
                AssignProtocol: _bindings.AssignProtocol,
                MarkProjectDirty: MarkProjectDirty,
                SyncCodingToPrimaryDamages: _bindings.SyncCodingToPrimaryDamages,
                PersistCodingEventsAsTrainingSamples: _bindings.PersistCodingEventsAsTrainingSamples,
                SetBaselineSignature: _bindings.SetBaselineSignature,
                SaveProjectAfterCoding: _bindings.SaveProjectAfterCoding,
                ShowOverlay: _bindings.ShowOverlay,
                ConfirmMissingPipeEnd: _bindings.ConfirmMissingPipeEnd));

        return result.Applied;
    }

    public bool ConfirmCanClose()
    {
        var result = CodingUnappliedChangesCloseWorkflow.Execute(
            new CodingUnappliedChangesCloseWorkflowRequest(
                IsCodingMode: _bindings.IsCodingMode(),
                HasCodingViewModel: _bindings.HasCodingViewModel(),
                Events: _bindings.GetEvents(),
                BaselineSignature: _bindings.GetBaselineSignature()),
            new CodingUnappliedChangesCloseWorkflowActions(
                BuildSignature: CodingEventsSignatureBuilder.Build,
                ConfirmWithSuspendedOverlay: () =>
                    _bindings.ConfirmUnappliedChanges(() => Apply(showOverlay: false))));

        return result.ShouldClose;
    }

    public void MarkProjectDirty()
        => _bindings.MarkProjectDirty(_bindings.GetHaltungRecord());
}
