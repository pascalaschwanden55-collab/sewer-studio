using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingManualCalibrationControllerActions(
    Action CloseToolsDropdown,
    Action<string> ApplyActiveToolSelection,
    Action ClearOverlayInfo,
    Action<CodingCalibrationToggleState> ApplyToggleControls,
    Action UpdateOverlayCursor,
    Action<bool> RedrawCodingCanvas,
    Func<NormalizedPoint, Point> MapToPixel,
    Action<string> ShowInvalidHint,
    Action<CodingManualCalibrationResult> ApplyManualResult,
    Action HideHint,
    Action EnableCodingSchemaOverlay);

public sealed class CodingManualCalibrationController
{
    private const int DefaultNominalDiameterMm = 300;

    private readonly CodingCalibrationStateController _calibrationState;
    private readonly CodingActiveToolNameStateController _activeToolNameState;
    private readonly CodingSchemaOverlayManagerOwner _schemaManager;
    private readonly ICodingSessionHost _sessionHost;
    private readonly ICodingOverlayToolHost _toolHost;
    private readonly CodingManualCalibrationControllerActions _actions;

    public CodingManualCalibrationController(
        CodingCalibrationStateController calibrationState,
        CodingActiveToolNameStateController activeToolNameState,
        CodingSchemaOverlayManagerOwner schemaManager,
        ICodingSessionHost sessionHost,
        ICodingOverlayToolHost toolHost,
        CodingManualCalibrationControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(calibrationState);
        ArgumentNullException.ThrowIfNull(activeToolNameState);
        ArgumentNullException.ThrowIfNull(schemaManager);
        ArgumentNullException.ThrowIfNull(sessionHost);
        ArgumentNullException.ThrowIfNull(toolHost);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CloseToolsDropdown);
        ArgumentNullException.ThrowIfNull(actions.ApplyActiveToolSelection);
        ArgumentNullException.ThrowIfNull(actions.ClearOverlayInfo);
        ArgumentNullException.ThrowIfNull(actions.ApplyToggleControls);
        ArgumentNullException.ThrowIfNull(actions.UpdateOverlayCursor);
        ArgumentNullException.ThrowIfNull(actions.RedrawCodingCanvas);
        ArgumentNullException.ThrowIfNull(actions.MapToPixel);
        ArgumentNullException.ThrowIfNull(actions.ShowInvalidHint);
        ArgumentNullException.ThrowIfNull(actions.ApplyManualResult);
        ArgumentNullException.ThrowIfNull(actions.HideHint);
        ArgumentNullException.ThrowIfNull(actions.EnableCodingSchemaOverlay);

        _calibrationState = calibrationState;
        _activeToolNameState = activeToolNameState;
        _schemaManager = schemaManager;
        _sessionHost = sessionHost;
        _toolHost = toolHost;
        _actions = actions;
    }

    public CodingCalibrationToggleWorkflowResult Toggle()
        => CodingCalibrationToggleWorkflow.Execute(
            new CodingCalibrationToggleWorkflowRequest(
                _toolHost.HasOverlayService,
                _sessionHost.HasViewModel,
                _calibrationState.IsCalibrating),
            new CodingCalibrationToggleWorkflowActions(
                CloseToolsDropdown: _actions.CloseToolsDropdown,
                SetCalibrationState: _calibrationState.SetCalibrating,
                ClearCalibrationStart: _calibrationState.ClearStart,
                SetActiveTool: activeTool => { _toolHost.SetActiveTool(activeTool); },
                SetActiveToolName: _activeToolNameState.Set,
                ApplyActiveToolSelection: _actions.ApplyActiveToolSelection,
                ClearCurrentOverlay: _sessionHost.ClearCurrentOverlay,
                ClearOverlayInfo: _actions.ClearOverlayInfo,
                ApplyToggleControls: _actions.ApplyToggleControls,
                UpdateOverlayCursor: _actions.UpdateOverlayCursor,
                RedrawCodingCanvas: _actions.RedrawCodingCanvas));

    public CodingManualCalibrationApplyWorkflowResult Apply(
        NormalizedPoint start,
        NormalizedPoint end)
        => CodingManualCalibrationApplyWorkflow.Execute(
            new CodingManualCalibrationApplyWorkflowRequest(_toolHost.HasOverlayService),
            new CodingManualCalibrationApplyWorkflowActions(
                BuildResult: () => CodingManualCalibrationPolicy.Build(
                    start,
                    end,
                    _actions.MapToPixel(start),
                    _actions.MapToPixel(end),
                    _toolHost.NominalDiameterMm ?? DefaultNominalDiameterMm),
                ApplyResult: result => CodingManualCalibrationWorkflow.Apply(
                    new CodingManualCalibrationWorkflowRequest(
                        result,
                        _activeToolNameState.ActiveToolName,
                        _schemaManager.IsActive),
                    new CodingManualCalibrationWorkflowActions(
                        ShowInvalidHint: _actions.ShowInvalidHint,
                        ClearCalibrationStart: _calibrationState.ClearStart,
                        SetOverlayCalibration: calibration => { _toolHost.SetCalibration(calibration); },
                        ApplySchemaCalibration: calibration => _schemaManager.Active?.ApplyCalibration(calibration),
                        ApplyManualResult: _actions.ApplyManualResult,
                        EndCalibrationMode: _calibrationState.Reset,
                        ClearActiveToolName: _activeToolNameState.Clear,
                        HideHint: _actions.HideHint,
                        UpdateOverlayCursor: _actions.UpdateOverlayCursor,
                        EnableCodingSchemaOverlay: _actions.EnableCodingSchemaOverlay))));
}
