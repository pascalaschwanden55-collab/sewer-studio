using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSchemaOverlayInputWorkflowTests
{
    [Fact]
    public void MouseDown_skips_when_schema_tool_is_not_selected()
    {
        var result = CodingSchemaOverlayInputWorkflow.MouseDown(
            new CodingSchemaOverlayMouseDownRequest(
                IsSchemaToolSelected: false,
                IsSchemaActive: false),
            MouseDownActions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingSchemaOverlayInputWorkflowOutcome.NotSelected, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void MouseDown_handles_missing_new_schema_without_placing()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayInputWorkflow.MouseDown(
            new CodingSchemaOverlayMouseDownRequest(
                IsSchemaToolSelected: true,
                IsSchemaActive: false),
            MouseDownActions(
                calls.Add,
                createAndActivateSchema: () =>
                {
                    calls.Add("create:false");
                    return false;
                }));

        Assert.Equal(CodingSchemaOverlayInputWorkflowOutcome.MissingSchema, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["create:false"], calls);
    }

    [Fact]
    public void MouseDown_activates_and_places_new_schema()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayInputWorkflow.MouseDown(
            new CodingSchemaOverlayMouseDownRequest(
                IsSchemaToolSelected: true,
                IsSchemaActive: false),
            MouseDownActions(calls.Add));

        Assert.Equal(CodingSchemaOverlayInputWorkflowOutcome.Activated, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["create:true", "place", "update"], calls);
    }

    [Fact]
    public void MouseDown_begins_drag_for_active_schema()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayInputWorkflow.MouseDown(
            new CodingSchemaOverlayMouseDownRequest(
                IsSchemaToolSelected: true,
                IsSchemaActive: true),
            MouseDownActions(
                calls.Add,
                resolveHandleId: () =>
                {
                    calls.Add("resolve");
                    return "h1";
                }));

        Assert.Equal(CodingSchemaOverlayInputWorkflowOutcome.DragStarted, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["resolve", "begin:h1", "drag", "capture", "update"], calls);
    }

    [Fact]
    public void MouseMove_skips_when_not_selected_or_not_active()
    {
        var notSelected = CodingSchemaOverlayInputWorkflow.MouseMove(
            new CodingSchemaOverlayMouseMoveRequest(
                IsSchemaToolSelected: false,
                IsSchemaActive: true,
                IsDragging: true),
            MouseMoveActions(_ => throw new InvalidOperationException("No action should run.")));
        var notActive = CodingSchemaOverlayInputWorkflow.MouseMove(
            new CodingSchemaOverlayMouseMoveRequest(
                IsSchemaToolSelected: true,
                IsSchemaActive: false,
                IsDragging: true),
            MouseMoveActions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingSchemaOverlayInputWorkflowOutcome.NotSelected, notSelected.Outcome);
        Assert.Equal(CodingSchemaOverlayInputWorkflowOutcome.NotSelected, notActive.Outcome);
        Assert.False(notSelected.Handled);
        Assert.False(notActive.Handled);
    }

    [Fact]
    public void MouseMove_handles_active_schema_without_drag()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayInputWorkflow.MouseMove(
            new CodingSchemaOverlayMouseMoveRequest(
                IsSchemaToolSelected: true,
                IsSchemaActive: true,
                IsDragging: false),
            MouseMoveActions(calls.Add));

        Assert.Equal(CodingSchemaOverlayInputWorkflowOutcome.NoDrag, result.Outcome);
        Assert.True(result.Handled);
        Assert.Empty(calls);
    }

    [Fact]
    public void MouseMove_updates_drag_and_overlay()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayInputWorkflow.MouseMove(
            new CodingSchemaOverlayMouseMoveRequest(
                IsSchemaToolSelected: true,
                IsSchemaActive: true,
                IsDragging: true),
            MouseMoveActions(calls.Add));

        Assert.Equal(CodingSchemaOverlayInputWorkflowOutcome.DragUpdated, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["drag", "update"], calls);
    }

    [Fact]
    public void MouseUp_completes_drag()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayInputWorkflow.MouseUp(
            new CodingSchemaOverlayMouseUpRequest(
                IsSchemaToolSelected: true,
                IsDragging: true),
            MouseUpActions(calls.Add));

        Assert.Equal(CodingSchemaOverlayInputWorkflowOutcome.DragCompleted, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["drag", "end", "release", "update"], calls);
    }

    private static CodingSchemaOverlayMouseDownActions MouseDownActions(
        Action<string> calls,
        Func<bool>? createAndActivateSchema = null,
        Func<string>? resolveHandleId = null)
        => new(
            CreateAndActivateSchema: createAndActivateSchema ?? (() =>
            {
                calls("create:true");
                return true;
            }),
            PlaceSchema: () => calls("place"),
            ResolveHandleId: resolveHandleId ?? (() => "h1"),
            BeginDrag: handleId => calls($"begin:{handleId}"),
            UpdateDrag: () => calls("drag"),
            CaptureMouse: () => calls("capture"),
            UpdateOverlay: () => calls("update"));

    private static CodingSchemaOverlayMouseMoveActions MouseMoveActions(Action<string> calls)
        => new(
            UpdateDrag: () => calls("drag"),
            UpdateOverlay: () => calls("update"));

    private static CodingSchemaOverlayMouseUpActions MouseUpActions(Action<string> calls)
        => new(
            UpdateDrag: () => calls("drag"),
            EndDrag: () => calls("end"),
            ReleaseMouseCapture: () => calls("release"),
            UpdateOverlay: () => calls("update"));
}
