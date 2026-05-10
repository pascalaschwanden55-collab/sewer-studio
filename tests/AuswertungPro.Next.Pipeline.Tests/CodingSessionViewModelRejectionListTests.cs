using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

// Tests fuer Slice 8a Pause-Confirm Step 1b — Sperrliste / Reject-Key
// auf CodingSessionViewModel. Mini-ADR:
// docs/adrs/2026-05-10-slice-8a-pause-confirm.md
public class CodingSessionViewModelRejectionListTests
{
    private static CodingSessionViewModel BuildVm()
        => new CodingSessionViewModel(
            new StubCodingSessionService(),
            new StubOverlayToolService(),
            new StubDialogService());

    // --- Initial State ---

    [Fact]
    public void NewVm_HasEmptyRejectionList()
    {
        var vm = BuildVm();
        Assert.Empty(vm.RejectedFindings);
        Assert.False(vm.IsRejected("BAB", "Riss", 12.5));
    }

    // --- Add + IsRejected (Happy-Path) ---

    [Fact]
    public void Add_ThenIsRejectedExactMatch_ReturnsTrue()
    {
        var vm = BuildVm();
        vm.AddRejection("BAB", "Riss", 12.5);
        Assert.True(vm.IsRejected("BAB", "Riss", 12.5));
        Assert.Single(vm.RejectedFindings);
    }

    [Fact]
    public void Add_DifferentCode_NotRejected()
    {
        var vm = BuildVm();
        vm.AddRejection("BAB", "Riss", 12.5);
        Assert.False(vm.IsRejected("BAC", "Riss", 12.5));
    }

    [Fact]
    public void Add_DifferentMeterFarOff_NotRejected()
    {
        var vm = BuildVm();
        vm.AddRejection("BAB", "Riss", 12.5);
        Assert.False(vm.IsRejected("BAB", "Riss", 20.0));
    }

    // --- Toleranz ±0.5m fuer echte Codes ---

    [Fact]
    public void IsRejected_MeterWithinTolerance_ReturnsTrue()
    {
        var vm = BuildVm();
        vm.AddRejection("BAB", "Riss", 12.5);
        Assert.True(vm.IsRejected("BAB", "Riss", 12.0));   // -0.5 inklusive
        Assert.True(vm.IsRejected("BAB", "Riss", 13.0));   // +0.5 inklusive
        Assert.True(vm.IsRejected("BAB", "Riss", 12.49));  // mittendrin
    }

    [Fact]
    public void IsRejected_MeterOutsideTolerance_ReturnsFalse()
    {
        var vm = BuildVm();
        vm.AddRejection("BAB", "Riss", 12.5);
        Assert.False(vm.IsRejected("BAB", "Riss", 11.99));  // -0.51
        Assert.False(vm.IsRejected("BAB", "Riss", 13.01));  // +0.51
    }

    // --- AI-Bucket: enge Toleranz +/-0.1m ---

    [Fact]
    public void IsRejected_AiCode_UsesNarrowTolerance()
    {
        var vm = BuildVm();
        vm.AddRejection("AI", "Riss", 12.5);
        Assert.True(vm.IsRejected("AI", "Riss", 12.45));   // -0.05 innerhalb +/-0.1
        Assert.True(vm.IsRejected("AI", "Riss", 12.59));   // +0.09 innerhalb +/-0.1
        Assert.True(vm.IsRejected("AI", "Riss", 12.4));    // -0.1 inklusive
        Assert.True(vm.IsRejected("AI", "Riss", 12.6));    // +0.1 inklusive
        Assert.False(vm.IsRejected("AI", "Riss", 12.3));   // -0.2 ausserhalb (echte Toleranz waere 0.5)
        Assert.False(vm.IsRejected("AI", "Riss", 12.7));   // +0.2 ausserhalb
    }

    // --- Label-Disambiguator: AI+Riss vs AI+Wurzel sind verschieden ---

    [Fact]
    public void IsRejected_AiBucket_LabelDisambiguates()
    {
        var vm = BuildVm();
        vm.AddRejection("AI", "Riss", 12.5);
        // Selbe Stelle, anderes Label → nicht gesperrt.
        Assert.False(vm.IsRejected("AI", "Wurzel", 12.5));
        // Selbe Stelle, gleiches Label → gesperrt.
        Assert.True(vm.IsRejected("AI", "Riss", 12.5));
    }

    [Fact]
    public void IsRejected_RealCode_LabelDisambiguates()
    {
        var vm = BuildVm();
        vm.AddRejection("BAB", "Riss laengs", 12.5);
        Assert.False(vm.IsRejected("BAB", "Riss quer", 12.5));
        Assert.True(vm.IsRejected("BAB", "Riss laengs", 12.5));
    }

    // --- Case-Insensitive Code- und Label-Match ---

    [Fact]
    public void IsRejected_CaseInsensitive_MatchesAllCasings()
    {
        var vm = BuildVm();
        vm.AddRejection("bab", "Riss", 12.5);
        Assert.True(vm.IsRejected("BAB", "RISS", 12.5));
        Assert.True(vm.IsRejected("BaB", "Riss", 12.5));
        Assert.True(vm.IsRejected("bab", "riss", 12.5));
    }

    [Fact]
    public void Add_TrimsWhitespaceCodeAndLabel()
    {
        var vm = BuildVm();
        vm.AddRejection("  BAB  ", "  Riss  ", 12.5);
        Assert.True(vm.IsRejected("BAB", "Riss", 12.5));
    }

    [Fact]
    public void Add_NullLabel_TreatedAsEmpty()
    {
        var vm = BuildVm();
        vm.AddRejection("BAB", null, 12.5);
        Assert.True(vm.IsRejected("BAB", null, 12.5));
        Assert.True(vm.IsRejected("BAB", "", 12.5));
        Assert.True(vm.IsRejected("BAB", "  ", 12.5));
    }

    // --- Idempotenz ---

    [Fact]
    public void AddRejection_SameKeyTwice_StoresOnce()
    {
        var vm = BuildVm();
        vm.AddRejection("BAB", "Riss", 12.5);
        vm.AddRejection("BAB", "Riss", 12.5);     // exakt gleich
        vm.AddRejection("bab", "RISS", 12.502);   // < 1cm Diff + andere Schreibung
        Assert.Single(vm.RejectedFindings);
    }

    [Fact]
    public void AddRejection_DifferentMetersBeyondCmTolerance_StoresBoth()
    {
        var vm = BuildVm();
        vm.AddRejection("BAB", "Riss", 12.50);
        vm.AddRejection("BAB", "Riss", 12.60);  // > 1cm Diff: separater Eintrag
        Assert.Equal(2, vm.RejectedFindings.Count);
    }

    [Fact]
    public void AddRejection_DifferentLabel_SameMeter_StoresBoth()
    {
        var vm = BuildVm();
        vm.AddRejection("AI", "Riss", 12.5);
        vm.AddRejection("AI", "Wurzel", 12.5);
        Assert.Equal(2, vm.RejectedFindings.Count);
    }

    // --- Empty/null Code = No-Op ---

    [Fact]
    public void AddRejection_EmptyCode_IsNoOp()
    {
        var vm = BuildVm();
        vm.AddRejection("", "Riss", 12.5);
        vm.AddRejection("   ", "Riss", 12.5);
        vm.AddRejection(null!, "Riss", 12.5);
        Assert.Empty(vm.RejectedFindings);
    }

    [Fact]
    public void IsRejected_EmptyCode_ReturnsFalse()
    {
        var vm = BuildVm();
        vm.AddRejection("BAB", "Riss", 12.5);
        Assert.False(vm.IsRejected("", "Riss", 12.5));
        Assert.False(vm.IsRejected("   ", "Riss", 12.5));
        Assert.False(vm.IsRejected(null!, "Riss", 12.5));
    }

    // --- MakeRejectionKey-Format ist stable ---

    [Fact]
    public void MakeRejectionKey_FormatIsStable()
    {
        Assert.Equal("BAB|RISS@12.50",  CodingSessionViewModel.MakeRejectionKey("BAB", "Riss", 12.5));
        Assert.Equal("BAB|RISS@12.50",  CodingSessionViewModel.MakeRejectionKey("bab", "riss", 12.5));
        Assert.Equal("BAB|RISS@12.50",  CodingSessionViewModel.MakeRejectionKey("  BAB  ", "  Riss  ", 12.5));
        Assert.Equal("BAC|@7.05",       CodingSessionViewModel.MakeRejectionKey("BAC", "", 7.05));
        Assert.Equal("BAC|@0.00",       CodingSessionViewModel.MakeRejectionKey("BAC", null, 0.0));
        Assert.Equal("AI|WURZEL@3.20",  CodingSessionViewModel.MakeRejectionKey("AI", "Wurzel", 3.2));
    }

    [Fact]
    public void MakeRejectionKey_UsesInvariantCulture()
    {
        var prev = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("BAB|RISS@12.50",
                CodingSessionViewModel.MakeRejectionKey("BAB", "Riss", 12.5));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = prev;
        }
    }

    // --- RejectedFindings exposes stable keys ---

    [Fact]
    public void RejectedFindings_ContainsStableKeys()
    {
        var vm = BuildVm();
        vm.AddRejection("BAB", "Riss", 12.5);
        vm.AddRejection("bac", "", 7.0);

        var keys = vm.RejectedFindings.OrderBy(k => k).ToList();
        Assert.Equal(new[] { "BAB|RISS@12.50", "BAC|@7.00" }, keys);
    }

    // --- Service-Stubs ---

    private sealed class StubCodingSessionService : ICodingSessionService
    {
        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => throw new NotImplementedException();
        public void PauseSession() => throw new NotImplementedException();
        public void ResumeSession() => throw new NotImplementedException();
        public void SetWaitingForInput() => throw new NotImplementedException();
        public void AbortSession(string reason) => throw new NotImplementedException();
        public ProtocolDocument CompleteSession() => throw new NotImplementedException();
        public IReadOnlyList<CodingEvent> GetOpenStreckenschaeden() => throw new NotImplementedException();
        public void CloseStreckenschaden(Guid eventId, double endMeter) => throw new NotImplementedException();
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public void MoveNext(double stepSizeM = 0.5) => throw new NotImplementedException();
        public void MovePrevious(double stepSizeM = 0.5) => throw new NotImplementedException();
        public void MoveToMeter(double meter) => throw new NotImplementedException();
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => throw new NotImplementedException();
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) => throw new NotImplementedException();
        public void RemoveEvent(Guid eventId) => throw new NotImplementedException();
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => Array.Empty<CodingEvent>();

        public event EventHandler<CodingSessionState>? StateChanged;
        public event EventHandler<double>? MeterChanged;
        public event EventHandler<CodingEvent>? EventAdded;

        public void RaiseStateChanged(CodingSessionState s) => StateChanged?.Invoke(this, s);
        public void RaiseMeterChanged(double m) => MeterChanged?.Invoke(this, m);
        public void RaiseEventAdded(CodingEvent ev) => EventAdded?.Invoke(this, ev);
    }

    private sealed class StubOverlayToolService : IOverlayToolService
    {
        public OverlayToolType ActiveTool { get; set; } = OverlayToolType.None;
        public LevelMode ActiveLevelMode { get; set; } = LevelMode.Water;
        public bool PipeBendSnapEnabled { get; set; }
        public event EventHandler<OverlayToolType>? ToolChanged;
        public void SetCalibration(PipeCalibration calibration) => throw new NotImplementedException();
        public PipeCalibration? Calibration => null;
        public bool IsCalibrated => false;
        public PipeCalibration? ApplyManualCalibration(NormalizedPoint start, NormalizedPoint end, double pixelDiameter, int nominalDiameterMm) => throw new NotImplementedException();
        public void BeginDraw(NormalizedPoint startPoint) => throw new NotImplementedException();
        public void UpdateDraw(NormalizedPoint currentPoint) => throw new NotImplementedException();
        public OverlayGeometry? EndDraw() => throw new NotImplementedException();
        public void CancelDraw() => throw new NotImplementedException();
        public bool IsDrawing => false;
        public bool IsMultiPointTool => false;
        public int RequiredPointCount => 0;
        public int DrawPointCount => 0;
        public bool AddDrawPoint(NormalizedPoint point) => throw new NotImplementedException();
        public IReadOnlyList<NormalizedPoint> DrawPoints => Array.Empty<NormalizedPoint>();
        public double PixelToMm(double normalizedPixels, double frameWidthPx) => 0;
        public double PointToClockHour(NormalizedPoint point) => 0;
        public NormalizedPoint? DrawStartPoint => null;
        public NormalizedPoint? DrawCurrentPoint => null;
        public OverlayGeometry? PreviewGeometry => null;
        public OverlayGeometry? BuildLevelGeometryFromSlider(double fillPercent, LevelMode mode) => null;
        public void ResizePipeCircle(double deltaNormalized) => throw new NotImplementedException();
        public void MovePipeCircle(NormalizedPoint newCenter) => throw new NotImplementedException();
        public void RaiseToolChanged(OverlayToolType t) => ToolChanged?.Invoke(this, t);
    }

    private sealed class StubDialogService : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public bool? ShowDialog(Window window) => null;
        public bool? ShowDialog(Func<Window> windowFactory) => null;
        public void Show(Window window) { }
        public MessageBoxResult ShowMessage(string text, string title, MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.None)
            => MessageBoxResult.None;
    }
}
