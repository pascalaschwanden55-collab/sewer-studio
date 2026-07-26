using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayInputMouseWorkflowTests
{
    [Fact]
    public void MouseDown_routes_eingabemarker_drawing_and_marks_event_handled()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputMouseWorkflow.MouseDown(
            new CodingOverlayInputMouseDownRequest(
                EingabemarkerState: CodingOverlayInputEingabemarkerState.Drawing,
                HasOverlayService: true,
                HasViewModel: true,
                IsActiveToolNone: false,
                IsMultiPointTool: false),
            MouseDownActions(calls));

        Assert.Equal(["eingabemarker-down", "handled"], calls);
        Assert.Equal(CodingOverlayInputMouseWorkflowOutcome.EingabemarkerHandled, result.Outcome);
    }

    [Fact]
    public void MouseDown_blocks_eingabemarker_input_without_canvas_work()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputMouseWorkflow.MouseDown(
            new CodingOverlayInputMouseDownRequest(
                EingabemarkerState: CodingOverlayInputEingabemarkerState.InputBlocked,
                HasOverlayService: true,
                HasViewModel: true,
                IsActiveToolNone: false,
                IsMultiPointTool: false),
            MouseDownActions(calls));

        Assert.Equal(["handled"], calls);
        Assert.Equal(CodingOverlayInputMouseWorkflowOutcome.InputBlocked, result.Outcome);
    }

    [Fact]
    public void MouseDown_routes_in_order_to_standard_handler()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputMouseWorkflow.MouseDown(
            new CodingOverlayInputMouseDownRequest(
                EingabemarkerState: CodingOverlayInputEingabemarkerState.Inactive,
                HasOverlayService: true,
                HasViewModel: true,
                IsActiveToolNone: false,
                IsMultiPointTool: false),
            MouseDownActions(
                calls,
                tryStartCalibration: () =>
                {
                    calls.Add("calibration");
                    return false;
                },
                tryHandleSchemaMouseDown: () =>
                {
                    calls.Add("schema");
                    return false;
                },
                handleStandardMouseDown: () => calls.Add("standard")));

        Assert.Equal(["calibration", "schema", "standard"], calls);
        Assert.Equal(CodingOverlayInputMouseWorkflowOutcome.StandardHandled, result.Outcome);
    }

    [Fact]
    public void MouseDown_routes_to_multi_point_before_standard()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputMouseWorkflow.MouseDown(
            new CodingOverlayInputMouseDownRequest(
                EingabemarkerState: CodingOverlayInputEingabemarkerState.Inactive,
                HasOverlayService: true,
                HasViewModel: true,
                IsActiveToolNone: false,
                IsMultiPointTool: true),
            MouseDownActions(
                calls,
                tryStartCalibration: () => false,
                tryHandleSchemaMouseDown: () => false,
                handleMultiPointMouseDown: () => calls.Add("multi"),
                handleStandardMouseDown: () => throw new InvalidOperationException("Standard should not run.")));

        Assert.Equal(["multi"], calls);
        Assert.Equal(CodingOverlayInputMouseWorkflowOutcome.MultiPointHandled, result.Outcome);
    }

    [Fact]
    public void MouseMove_routes_preview_schema_multipoint_then_standard()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputMouseWorkflow.MouseMove(
            new CodingOverlayInputMouseMoveRequest(
                IsEingabemarkerDrawingWithPreview: false,
                HasOverlayService: true,
                HasViewModel: true),
            MouseMoveActions(
                calls,
                tryPreviewCalibration: () =>
                {
                    calls.Add("calibration");
                    return false;
                },
                tryHandleSchemaMouseMove: () =>
                {
                    calls.Add("schema");
                    return false;
                },
                tryHandleMultiPointMouseMove: () =>
                {
                    calls.Add("multi");
                    return false;
                },
                tryHandleStandardMouseMove: () =>
                {
                    calls.Add("standard");
                    return true;
                }));

        Assert.Equal(["calibration", "schema", "multi", "standard"], calls);
        Assert.Equal(CodingOverlayInputMouseWorkflowOutcome.StandardHandled, result.Outcome);
    }

    [Fact]
    public void MouseUp_routes_eingabemarker_drawing_and_marks_event_handled()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputMouseWorkflow.MouseUp(
            new CodingOverlayInputMouseUpRequest(
                IsEingabemarkerDrawing: true,
                HasOverlayService: true,
                HasViewModel: true),
            MouseUpActions(calls));

        Assert.Equal(["eingabemarker-up", "handled"], calls);
        Assert.Equal(CodingOverlayInputMouseWorkflowOutcome.EingabemarkerHandled, result.Outcome);
    }

    [Fact]
    public void MouseUp_routes_calibration_schema_then_standard()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputMouseWorkflow.MouseUp(
            new CodingOverlayInputMouseUpRequest(
                IsEingabemarkerDrawing: false,
                HasOverlayService: true,
                HasViewModel: true),
            MouseUpActions(
                calls,
                tryFinishCalibration: () =>
                {
                    calls.Add("calibration");
                    return false;
                },
                tryHandleSchemaMouseUp: () =>
                {
                    calls.Add("schema");
                    return false;
                },
                tryHandleStandardMouseUp: () =>
                {
                    calls.Add("standard");
                    return true;
                }));

        Assert.Equal(["calibration", "schema", "standard"], calls);
        Assert.Equal(CodingOverlayInputMouseWorkflowOutcome.StandardHandled, result.Outcome);
    }

    private static CodingOverlayInputMouseDownActions MouseDownActions(
        List<string> calls,
        Action? handleEingabemarkerMouseDown = null,
        Action? markHandled = null,
        Func<bool>? tryStartCalibration = null,
        Func<bool>? tryHandleSchemaMouseDown = null,
        Action? handleMultiPointMouseDown = null,
        Action? handleStandardMouseDown = null)
        => new(
            HandleEingabemarkerMouseDown: handleEingabemarkerMouseDown ?? (() => calls.Add("eingabemarker-down")),
            MarkHandled: markHandled ?? (() => calls.Add("handled")),
            TryStartCalibration: tryStartCalibration ?? (() => throw new InvalidOperationException("Calibration should not run.")),
            TryHandleSchemaMouseDown: tryHandleSchemaMouseDown ?? (() => throw new InvalidOperationException("Schema should not run.")),
            HandleMultiPointMouseDown: handleMultiPointMouseDown ?? (() => throw new InvalidOperationException("Multi-point should not run.")),
            HandleStandardMouseDown: handleStandardMouseDown ?? (() => throw new InvalidOperationException("Standard should not run.")));

    private static CodingOverlayInputMouseMoveActions MouseMoveActions(
        List<string> calls,
        Action? handleEingabemarkerMouseMove = null,
        Func<bool>? tryPreviewCalibration = null,
        Func<bool>? tryHandleSchemaMouseMove = null,
        Func<bool>? tryHandleMultiPointMouseMove = null,
        Func<bool>? tryHandleStandardMouseMove = null)
        => new(
            HandleEingabemarkerMouseMove: handleEingabemarkerMouseMove ?? (() => calls.Add("eingabemarker-move")),
            TryPreviewCalibration: tryPreviewCalibration ?? (() => throw new InvalidOperationException("Calibration should not run.")),
            TryHandleSchemaMouseMove: tryHandleSchemaMouseMove ?? (() => throw new InvalidOperationException("Schema should not run.")),
            TryHandleMultiPointMouseMove: tryHandleMultiPointMouseMove ?? (() => throw new InvalidOperationException("Multi-point should not run.")),
            TryHandleStandardMouseMove: tryHandleStandardMouseMove ?? (() => throw new InvalidOperationException("Standard should not run.")));

    private static CodingOverlayInputMouseUpActions MouseUpActions(
        List<string> calls,
        Action? handleEingabemarkerMouseUp = null,
        Action? markHandled = null,
        Func<bool>? tryFinishCalibration = null,
        Func<bool>? tryHandleSchemaMouseUp = null,
        Func<bool>? tryHandleStandardMouseUp = null)
        => new(
            HandleEingabemarkerMouseUp: handleEingabemarkerMouseUp ?? (() => calls.Add("eingabemarker-up")),
            MarkHandled: markHandled ?? (() => calls.Add("handled")),
            TryFinishCalibration: tryFinishCalibration ?? (() => throw new InvalidOperationException("Calibration should not run.")),
            TryHandleSchemaMouseUp: tryHandleSchemaMouseUp ?? (() => throw new InvalidOperationException("Schema should not run.")),
            TryHandleStandardMouseUp: tryHandleStandardMouseUp ?? (() => throw new InvalidOperationException("Standard should not run.")));
}
