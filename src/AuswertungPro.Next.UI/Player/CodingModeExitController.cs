using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingModeExitController
{
    void Exit();
}

public sealed record CodingModeExitControllerBindings(
    Func<bool> IsCodingMode,
    Action<bool> SetCodingMode,
    Func<CodingModeExitFinalizationWorkflowRequest> CreateFinalizationRequest,
    CodingModeExitFinalizationWorkflowActions FinalizationActions,
    Func<CodingModeExitTeardownWorkflowRequest> CreateTeardownRequest,
    CodingModeExitTeardownWorkflowActions TeardownActions);

public sealed class CodingModeExitController : ICodingModeExitController
{
    private readonly CodingModeExitControllerBindings _bindings;

    public CodingModeExitController(CodingModeExitControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        _bindings = bindings;
    }

    public void Exit()
        => CodingModeExitCommandWorkflow.Execute(
            new CodingModeExitCommandRequest(_bindings.IsCodingMode()),
            new CodingModeExitCommandActions(
                SetCodingMode: _bindings.SetCodingMode,
                FinalizeExit: FinalizeExit,
                Teardown: Teardown));

    private CodingModeExitFinalizationWorkflowResult FinalizeExit()
        => CodingModeExitFinalizationWorkflow.Execute(
            _bindings.CreateFinalizationRequest(),
            _bindings.FinalizationActions);

    private void Teardown()
        => CodingModeExitTeardownWorkflow.Execute(
            _bindings.CreateTeardownRequest(),
            _bindings.TeardownActions);
}
