using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSchemaOverlayControllerTests
{
    [Fact]
    public void MouseDown_creates_places_and_renders_selected_schema()
    {
        var calls = new List<string>();
        var manager = new CodingSchemaOverlayManagerOwner();
        var typeState = new CodingSchemaTypeStateController();
        typeState.Set(SchemaType.PipeBend);
        var sessionHost = new FakeCodingSessionHost
        {
            HasViewModel = true,
            OverlayChanged = _ => calls.Add("set-overlay")
        };
        var controller = CreateController(
            manager,
            typeState,
            sessionHost,
            new FakeCodingOverlayToolHost
            {
                HasOverlayService = true,
                ActiveTool = OverlayToolType.PipeBend
            },
            calls);

        var handled = controller.MouseDown(new NormalizedPoint(0.4, 0.6));

        Assert.True(handled);
        Assert.IsType<PipeBendSchema>(manager.Active);
        Assert.NotNull(sessionHost.CurrentOverlay);
        Assert.Equal(OverlayToolType.PipeBend, sessionHost.CurrentOverlay.ToolType);
        Assert.Equal(
            ["set-overlay", "overlay-info:True", "create:True", "clear-canvas", "ai", "reference", "badge", "render-schema"],
            calls);
    }

    [Fact]
    public void MouseWheel_adjusts_active_bend_and_marks_event_handled_after_rendering()
    {
        var calls = new List<string>();
        var manager = new CodingSchemaOverlayManagerOwner();
        var typeState = new CodingSchemaTypeStateController();
        typeState.Set(SchemaType.PipeBend);
        var sessionHost = new FakeCodingSessionHost { HasViewModel = true };
        var controller = CreateController(
            manager,
            typeState,
            sessionHost,
            new FakeCodingOverlayToolHost
            {
                HasOverlayService = true,
                ActiveTool = OverlayToolType.PipeBend
            },
            calls);
        controller.MouseDown(new NormalizedPoint(0.5, 0.5));
        calls.Clear();

        var handled = controller.MouseWheel(120, () => calls.Add("handled"));

        Assert.True(handled);
        Assert.Equal(50, Assert.IsType<PipeBendSchema>(manager.Active).AngleDeg);
        Assert.Equal("handled", calls[^1]);
        Assert.Contains("render-schema", calls);
    }

    [Fact]
    public void Clear_removes_schema_and_session_overlay_before_optional_redraw()
    {
        var calls = new List<string>();
        var manager = new CodingSchemaOverlayManagerOwner();
        manager.Activate(new PipeBendSchema());
        var sessionHost = new FakeCodingSessionHost
        {
            HasViewModel = true,
            CurrentOverlay = new OverlayGeometry(),
            OverlayChanged = overlay => calls.Add(overlay is null ? "clear-overlay" : "set-overlay")
        };
        var controller = CreateController(
            manager,
            new CodingSchemaTypeStateController(),
            sessionHost,
            new FakeCodingOverlayToolHost(),
            calls);

        controller.Clear(redraw: true);

        Assert.Null(manager.Active);
        Assert.Null(sessionHost.CurrentOverlay);
        Assert.Equal(
            ["clear-overlay", "create:False", "overlay-info:False", "redraw:False"],
            calls);
    }

    private static CodingSchemaOverlayController CreateController(
        CodingSchemaOverlayManagerOwner manager,
        CodingSchemaTypeStateController typeState,
        ICodingSessionHost sessionHost,
        ICodingOverlayToolHost toolHost,
        ICollection<string> calls)
        => new(
            manager,
            typeState,
            sessionHost,
            toolHost,
            new CodingSchemaOverlayControllerActions(
                CaptureMouse: () => calls.Add("capture"),
                ReleaseMouseCapture: () => calls.Add("release"),
                UpdateOverlayInfo: overlay => calls.Add($"overlay-info:{overlay is not null}"),
                SetCreateEventEnabled: enabled => calls.Add($"create:{enabled}"),
                ClearTransientCodingCanvas: () => calls.Add("clear-canvas"),
                RenderAiOverlays: () => calls.Add("ai"),
                RenderReferenceDn: () => calls.Add("reference"),
                UpdateToolBadge: () => calls.Add("badge"),
                RenderActiveCodingSchema: () => calls.Add("render-schema"),
                RedrawCodingCanvas: includeManualOverlay => calls.Add($"redraw:{includeManualOverlay}")));

    private sealed class FakeCodingSessionHost : ICodingSessionHost
    {
        public bool HasViewModel { get; init; }
        public bool IsRunningOrPaused => false;
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public OverlayGeometry? CurrentOverlay { get; set; }
        public ObservableCollection<CodingEvent>? EventCollection => null;
        public IEnumerable<CodingEvent> Events => [];
        public CodingEvent? SelectedDefect => null;
        public string? HaltungName => null;
        public string? VideoPath => null;
        public TimeSpan? CurrentVideoTime => null;
        public string SelectedCode => string.Empty;
        public string SelectedCodeDescription => string.Empty;
        public Action<OverlayGeometry?> OverlayChanged { get; init; } = _ => { };

        public void SetCurrentVideoTime(TimeSpan videoTime) { }
        public void SelectDefect(CodingEvent? codingEvent) { }
        public void ClearSelectedDefect() { }
        public void SetCurrentOverlay(OverlayGeometry? overlay)
        {
            CurrentOverlay = overlay;
            OverlayChanged(overlay);
        }

        public void ClearCurrentOverlay() => SetCurrentOverlay(null);
        public void ClearSelectedCode() { }
        public void BeginOverlayDraw(NormalizedPoint point) { }
        public void UpdateOverlayDraw(NormalizedPoint point) { }
        public void CompleteOverlayDraw(NormalizedPoint point) { }
        public bool AddMultiPointOverlayPoint(NormalizedPoint point) => false;
        public void UpdateMultiPointOverlayPreview(NormalizedPoint point) { }
        public bool ExecuteMoveNext() => false;
        public bool ExecuteMovePrevious() => false;
        public bool ExecuteAcceptDefect() => false;
        public bool ExecuteEditDefect() => false;
        public bool ExecuteStartSession(HaltungRecord? haltung) => false;
        public bool ExecuteJumpToDefect(CodingEvent? codingEvent) => false;
    }

    private sealed class FakeCodingOverlayToolHost : ICodingOverlayToolHost
    {
        public bool HasOverlayService { get; init; }
        public OverlayToolType ActiveTool { get; set; }
        public LevelMode ActiveLevelMode { get; set; } = LevelMode.Deposit;
        public bool PipeBendSnapEnabled { get; init; }
        public bool IsDrawing => false;
        public bool IsMultiPointTool => false;
        public int DrawPointCount => 0;
        public PipeCalibration? Calibration { get; init; }
        public int? NominalDiameterMm => Calibration?.NominalDiameterMm;
        public bool IsCalibrated => Calibration is not null;

        public bool SetActiveTool(OverlayToolType tool)
        {
            ActiveTool = tool;
            return true;
        }

        public bool SetActiveLevelMode(LevelMode mode)
        {
            ActiveLevelMode = mode;
            return true;
        }

        public bool SetCalibration(PipeCalibration calibration) => true;
        public bool CancelDraw() => true;
    }
}
