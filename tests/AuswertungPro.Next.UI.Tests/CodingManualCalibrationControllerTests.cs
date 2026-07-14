using System.Collections.ObjectModel;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingManualCalibrationControllerTests
{
    [Fact]
    public void Toggle_is_side_effect_free_when_required_runtime_is_missing()
    {
        var calls = new List<string>();
        var controller = CreateController(
            new CodingCalibrationStateController(),
            new CodingActiveToolNameStateController(),
            new CodingSchemaOverlayManagerOwner(),
            new FakeCodingSessionHost { HasViewModel = false },
            new FakeCodingOverlayToolHost { HasOverlayService = true },
            calls);

        var result = controller.Toggle();

        Assert.Equal(CodingCalibrationToggleWorkflowOutcome.PrerequisitesMissing, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Toggle_starts_calibration_and_clears_previous_overlay()
    {
        var calls = new List<string>();
        var calibrationState = new CodingCalibrationStateController();
        var activeToolState = new CodingActiveToolNameStateController();
        var sessionHost = new FakeCodingSessionHost
        {
            HasViewModel = true,
            CurrentOverlay = new OverlayGeometry(),
            OverlayChanged = overlay => calls.Add(overlay is null ? "clear-current" : "set-current")
        };
        var toolHost = new FakeCodingOverlayToolHost { HasOverlayService = true };
        var controller = CreateController(
            calibrationState,
            activeToolState,
            new CodingSchemaOverlayManagerOwner(),
            sessionHost,
            toolHost,
            calls);

        var result = controller.Toggle();

        Assert.Equal(CodingCalibrationToggleWorkflowOutcome.Applied, result.Outcome);
        Assert.True(calibrationState.IsCalibrating);
        Assert.Equal(CodingCalibrationTogglePolicy.CalibrateButtonName, activeToolState.ActiveToolName);
        Assert.Equal(OverlayToolType.None, toolHost.ActiveTool);
        Assert.Null(sessionHost.CurrentOverlay);
        Assert.Equal(
            [
                "close-tools",
                "selection:Kalibrieren",
                "clear-current",
                "clear-info",
                "toggle:True",
                "cursor",
                "redraw:False"
            ],
            calls);
    }

    [Fact]
    public void Apply_uses_nominal_diameter_updates_schema_and_resets_calibration_state()
    {
        var calls = new List<string>();
        var calibrationState = new CodingCalibrationStateController();
        calibrationState.SetCalibrating(true);
        calibrationState.SetStart(new NormalizedPoint(0.2, 0.4));
        var activeToolState = new CodingActiveToolNameStateController();
        activeToolState.Set(CodingCalibrationTogglePolicy.CalibrateButtonName);
        var schemaManager = new CodingSchemaOverlayManagerOwner();
        schemaManager.Activate(new PipeBendSchema());
        schemaManager.Place(new NormalizedPoint(0.5, 0.4));
        var toolHost = new FakeCodingOverlayToolHost
        {
            HasOverlayService = true,
            NominalDiameterMm = 400
        };
        var controller = CreateController(
            calibrationState,
            activeToolState,
            schemaManager,
            new FakeCodingSessionHost { HasViewModel = true },
            toolHost,
            calls);

        var result = controller.Apply(
            new NormalizedPoint(0.2, 0.4),
            new NormalizedPoint(0.8, 0.4));

        Assert.Equal(CodingManualCalibrationApplyWorkflowOutcome.Applied, result.Outcome);
        Assert.NotNull(toolHost.Calibration);
        Assert.Equal(400, toolHost.Calibration.NominalDiameterMm);
        Assert.Equal(0.3, schemaManager.Active!.PipeRadius, precision: 3);
        Assert.Equal(0.5, schemaManager.Active.PipeCenter.X, precision: 3);
        Assert.False(calibrationState.IsCalibrating);
        Assert.Null(calibrationState.Start);
        Assert.Null(activeToolState.ActiveToolName);
        Assert.Equal(
            ["manual-result:400", "hide-hint", "cursor", "schema-overlay"],
            calls);
    }

    private static CodingManualCalibrationController CreateController(
        CodingCalibrationStateController calibrationState,
        CodingActiveToolNameStateController activeToolState,
        CodingSchemaOverlayManagerOwner schemaManager,
        ICodingSessionHost sessionHost,
        ICodingOverlayToolHost toolHost,
        ICollection<string> calls)
        => new(
            calibrationState,
            activeToolState,
            schemaManager,
            sessionHost,
            toolHost,
            new CodingManualCalibrationControllerActions(
                CloseToolsDropdown: () => calls.Add("close-tools"),
                ApplyActiveToolSelection: label => calls.Add($"selection:{label}"),
                ClearOverlayInfo: () => calls.Add("clear-info"),
                ApplyToggleControls: state => calls.Add($"toggle:{state.IsCalibrating}"),
                UpdateOverlayCursor: () => calls.Add("cursor"),
                RedrawCodingCanvas: includeManualOverlay => calls.Add($"redraw:{includeManualOverlay}"),
                MapToPixel: point => new Point(point.X * 1000, point.Y * 1000),
                ShowInvalidHint: text => calls.Add($"invalid:{text}"),
                ApplyManualResult: result => calls.Add($"manual-result:{result.Calibration!.NominalDiameterMm}"),
                HideHint: () => calls.Add("hide-hint"),
                EnableCodingSchemaOverlay: () => calls.Add("schema-overlay")));

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
        public OverlayToolType ActiveTool { get; private set; }
        public LevelMode ActiveLevelMode { get; private set; } = LevelMode.Deposit;
        public bool PipeBendSnapEnabled => false;
        public bool IsDrawing => false;
        public bool IsMultiPointTool => false;
        public int DrawPointCount => 0;
        public PipeCalibration? Calibration { get; private set; }
        public int? NominalDiameterMm { get; init; }
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

        public bool SetCalibration(PipeCalibration calibration)
        {
            Calibration = calibration;
            return true;
        }

        public bool CancelDraw() => true;
    }
}
