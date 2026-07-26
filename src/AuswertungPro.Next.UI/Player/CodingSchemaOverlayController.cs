using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingSchemaOverlayControllerActions(
    Action CaptureMouse,
    Action ReleaseMouseCapture,
    Action<OverlayGeometry?> UpdateOverlayInfo,
    Action<bool> SetCreateEventEnabled,
    Action ClearTransientCodingCanvas,
    Action RenderAiOverlays,
    Action RenderReferenceDn,
    Action UpdateToolBadge,
    Action RenderActiveCodingSchema,
    Action<bool> RedrawCodingCanvas);

public sealed class CodingSchemaOverlayController
{
    private const double HandleHitTestThreshold = 0.035;

    private readonly CodingSchemaOverlayManagerOwner _manager;
    private readonly CodingSchemaTypeStateController _typeState;
    private readonly ICodingSessionHost _sessionHost;
    private readonly ICodingOverlayToolHost _toolHost;
    private readonly CodingSchemaOverlayControllerActions _actions;

    public CodingSchemaOverlayController(
        CodingSchemaOverlayManagerOwner manager,
        CodingSchemaTypeStateController typeState,
        ICodingSessionHost sessionHost,
        ICodingOverlayToolHost toolHost,
        CodingSchemaOverlayControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(typeState);
        ArgumentNullException.ThrowIfNull(sessionHost);
        ArgumentNullException.ThrowIfNull(toolHost);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CaptureMouse);
        ArgumentNullException.ThrowIfNull(actions.ReleaseMouseCapture);
        ArgumentNullException.ThrowIfNull(actions.UpdateOverlayInfo);
        ArgumentNullException.ThrowIfNull(actions.SetCreateEventEnabled);
        ArgumentNullException.ThrowIfNull(actions.ClearTransientCodingCanvas);
        ArgumentNullException.ThrowIfNull(actions.RenderAiOverlays);
        ArgumentNullException.ThrowIfNull(actions.RenderReferenceDn);
        ArgumentNullException.ThrowIfNull(actions.UpdateToolBadge);
        ArgumentNullException.ThrowIfNull(actions.RenderActiveCodingSchema);
        ArgumentNullException.ThrowIfNull(actions.RedrawCodingCanvas);

        _manager = manager;
        _typeState = typeState;
        _sessionHost = sessionHost;
        _toolHost = toolHost;
        _actions = actions;
    }

    public OverlayGeometry? BuildGeometry()
        => CodingSchemaOverlayBuilder.BuildGeometry(_manager.Active);

    public bool MouseDown(NormalizedPoint point)
        => CodingSchemaOverlayInputWorkflow.MouseDown(
            new CodingSchemaOverlayMouseDownRequest(
                IsToolSelected(),
                _manager.IsActive),
            new CodingSchemaOverlayMouseDownActions(
                CreateAndActivateSchema,
                PlaceSchema: () => _manager.Place(point),
                ResolveHandleId: () =>
                    _manager.HitTest(point, HandleHitTestThreshold) ?? ResolveDefaultHandleId(),
                BeginDrag: _manager.BeginDrag,
                UpdateDrag: () => _manager.UpdateDrag(point),
                CaptureMouse: _actions.CaptureMouse,
                UpdateOverlay: () => Update(enableCreateEvent: true)))
        .Handled;

    public bool MouseMove(NormalizedPoint point)
        => CodingSchemaOverlayInputWorkflow.MouseMove(
            new CodingSchemaOverlayMouseMoveRequest(
                IsToolSelected(),
                _manager.IsActive,
                _manager.IsDragging),
            new CodingSchemaOverlayMouseMoveActions(
                UpdateDrag: () => _manager.UpdateDrag(point),
                UpdateOverlay: () => Update(enableCreateEvent: true)))
        .Handled;

    public bool MouseUp(NormalizedPoint point)
        => CodingSchemaOverlayInputWorkflow.MouseUp(
            new CodingSchemaOverlayMouseUpRequest(
                IsToolSelected(),
                _manager.IsDragging),
            new CodingSchemaOverlayMouseUpActions(
                UpdateDrag: () => _manager.UpdateDrag(point),
                EndDrag: _manager.EndDrag,
                ReleaseMouseCapture: _actions.ReleaseMouseCapture,
                UpdateOverlay: () => Update(enableCreateEvent: true)))
        .Handled;

    public void Update(bool enableCreateEvent)
    {
        OverlayGeometry? overlay = null;

        CodingSchemaOverlayUpdateWorkflow.Execute(
            new CodingSchemaOverlayUpdateRequest(
                _sessionHost.HasViewModel,
                enableCreateEvent),
            new CodingSchemaOverlayUpdateActions(
                BuildSetAndReportOverlay: () =>
                {
                    overlay = BuildGeometry();
                    _sessionHost.SetCurrentOverlay(overlay);
                    return overlay is not null;
                },
                UpdateOverlayInfo: () => _actions.UpdateOverlayInfo(overlay),
                SetCreateEventEnabled: _actions.SetCreateEventEnabled,
                ClearTransientCodingCanvas: _actions.ClearTransientCodingCanvas,
                RenderAiOverlays: _actions.RenderAiOverlays,
                RenderReferenceDn: _actions.RenderReferenceDn,
                UpdateToolBadge: _actions.UpdateToolBadge,
                RenderActiveCodingSchema: _actions.RenderActiveCodingSchema));
    }

    public void Clear(bool redraw)
        => CodingSchemaOverlayClearWorkflow.Execute(
            new CodingSchemaOverlayClearRequest(redraw),
            new CodingSchemaOverlayClearActions(
                CancelSchema: _manager.Cancel,
                ClearCurrentOverlay: _sessionHost.ClearCurrentOverlay,
                SetCreateEventEnabled: _actions.SetCreateEventEnabled,
                ClearOverlayInfo: () => _actions.UpdateOverlayInfo(null),
                RedrawCodingCanvas: _actions.RedrawCodingCanvas));

    public bool MouseWheel(int wheelDelta, Action markHandled)
    {
        ArgumentNullException.ThrowIfNull(markHandled);

        var bend = _manager.Active as PipeBendSchema;
        return CodingSchemaOverlayMouseWheelWorkflow.Execute(
            new CodingSchemaOverlayMouseWheelRequest(
                bend is not null,
                _manager.IsActive,
                wheelDelta),
            new CodingSchemaOverlayMouseWheelActions(
                AdjustAngle: angleDelta => bend?.AdjustAngle(angleDelta),
                UpdateOverlay: () => Update(enableCreateEvent: true),
                MarkHandled: markHandled))
            .Handled;
    }

    private bool IsToolSelected()
        => _typeState.ActiveSchemaType.HasValue
           && _toolHost.ActiveTool is OverlayToolType.PipeBend or OverlayToolType.Level;

    private bool CreateAndActivateSchema()
    {
        var createResult = CodingSchemaOverlayCreateWorkflow.Execute(
            new CodingSchemaOverlayCreateRequest(_toolHost.HasOverlayService),
            new CodingSchemaOverlayCreateActions(
                CreateSchema: () => CodingSchemaOverlayBuilder.Create(
                    _typeState.ActiveSchemaType,
                    _toolHost.PipeBendSnapEnabled,
                    _toolHost.ActiveLevelMode)));

        return CodingSchemaOverlayActivationWorkflow.Execute(
                new CodingSchemaOverlayActivationWorkflowRequest(createResult.Schema),
                new CodingSchemaOverlayActivationWorkflowActions(
                    schema => _manager.Activate(schema, _toolHost.Calibration)))
            .Activated;
    }

    private string ResolveDefaultHandleId()
        => CodingSchemaOverlayBuilder.GetDefaultHandleId(_typeState.ActiveSchemaType);
}
