using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

// Tests fuer Slice 8a Pause-Confirm Step 1a — ConfirmationFlow API
// auf CodingSessionViewModel. Mini-ADR:
// docs/adrs/2026-05-10-slice-8a-pause-confirm.md
public class CodingSessionViewModelPauseConfirmTests
{
    private static CodingSessionViewModel BuildVm()
        => new CodingSessionViewModel(
            new StubCodingSessionService(),
            new StubOverlayToolService(),
            new StubDialogService());

    private static CodingEvent NewEvent()
        => new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BAB", Beschreibung = "Riss" },
            MeterAtCapture = 12.5
        };

    // --- Initial State ---

    [Fact]
    public void NewVm_HasNoPendingConfirmation()
    {
        var vm = BuildVm();
        Assert.False(vm.IsAwaitingUserDecision);
        Assert.Null(vm.PendingConfirmationEvent);
        Assert.Null(vm.PendingConfirmationConfidence);
        Assert.False(vm.PendingConfirmationIsRed);
    }

    // --- Happy-Path: BeginAsync + Complete ---

    [Fact]
    public async Task BeginConfirmation_ThenCompleteAccepted_ReturnsAccepted()
    {
        var vm = BuildVm();
        var ev = NewEvent();
        var task = vm.BeginConfirmationAsync(ev, confidence: 0.72, isRed: false, CancellationToken.None);

        Assert.True(vm.IsAwaitingUserDecision);
        Assert.Same(ev, vm.PendingConfirmationEvent);
        Assert.Equal(0.72, vm.PendingConfirmationConfidence);
        Assert.False(vm.PendingConfirmationIsRed);

        vm.CompleteConfirmation(CodingUserDecision.Accepted);
        var decision = await task;

        Assert.Equal(CodingUserDecision.Accepted, decision);
        Assert.False(vm.IsAwaitingUserDecision);
        Assert.Null(vm.PendingConfirmationEvent);
        Assert.Null(vm.PendingConfirmationConfidence);
    }

    [Fact]
    public async Task BeginConfirmation_RedZone_PropagatesIsRed()
    {
        var vm = BuildVm();
        var task = vm.BeginConfirmationAsync(NewEvent(), confidence: 0.40, isRed: true, CancellationToken.None);

        Assert.True(vm.PendingConfirmationIsRed);

        vm.CompleteConfirmation(CodingUserDecision.Rejected);
        Assert.Equal(CodingUserDecision.Rejected, await task);
    }

    [Fact]
    public async Task BeginConfirmation_AcceptWithEdit_RoundTripsDecision()
    {
        var vm = BuildVm();
        var task = vm.BeginConfirmationAsync(NewEvent(), 0.65, false, CancellationToken.None);
        vm.CompleteConfirmation(CodingUserDecision.AcceptedWithEdit);
        Assert.Equal(CodingUserDecision.AcceptedWithEdit, await task);
    }

    // --- Cancel ---

    [Fact]
    public async Task BeginConfirmation_TokenCanceled_ThrowsOperationCanceled()
    {
        var vm = BuildVm();
        using var cts = new CancellationTokenSource();
        var task = vm.BeginConfirmationAsync(NewEvent(), 0.60, false, cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
        Assert.False(vm.IsAwaitingUserDecision);
        Assert.Null(vm.PendingConfirmationEvent);
    }

    // --- Re-Entrancy ---

    [Fact]
    public void BeginConfirmation_WhilePending_Throws()
    {
        var vm = BuildVm();
        _ = vm.BeginConfirmationAsync(NewEvent(), 0.70, false, CancellationToken.None);

        // Wirft synchron (vor dem await), daher Assert.Throws statt ThrowsAsync.
        Assert.Throws<InvalidOperationException>(
            () => { _ = vm.BeginConfirmationAsync(NewEvent(), 0.50, true, CancellationToken.None); });
    }

    [Fact]
    public async Task BeginConfirmation_AfterComplete_StartsNewPending()
    {
        var vm = BuildVm();
        var first = vm.BeginConfirmationAsync(NewEvent(), 0.70, false, CancellationToken.None);
        vm.CompleteConfirmation(CodingUserDecision.Accepted);
        await first;

        // Zweite Confirmation muss problemlos starten
        var second = vm.BeginConfirmationAsync(NewEvent(), 0.40, true, CancellationToken.None);
        Assert.True(vm.IsAwaitingUserDecision);
        vm.CompleteConfirmation(CodingUserDecision.Rejected);
        Assert.Equal(CodingUserDecision.Rejected, await second);
    }

    // --- CompleteConfirmation ohne pending ---

    [Fact]
    public void CompleteConfirmation_WithoutPending_IsNoOp()
    {
        var vm = BuildVm();
        // Darf nicht werfen
        vm.CompleteConfirmation(CodingUserDecision.Accepted);
        Assert.False(vm.IsAwaitingUserDecision);
    }

    [Fact]
    public async Task CompleteConfirmation_DoubleCall_SecondIsNoOp()
    {
        var vm = BuildVm();
        var task = vm.BeginConfirmationAsync(NewEvent(), 0.65, false, CancellationToken.None);
        vm.CompleteConfirmation(CodingUserDecision.Accepted);
        // Zweiter Aufruf darf nichts werfen und nichts mehr veraendern
        vm.CompleteConfirmation(CodingUserDecision.Rejected);
        Assert.Equal(CodingUserDecision.Accepted, await task);
    }

    // --- Argument-Validation ---

    [Fact]
    public void BeginConfirmation_NullEvent_Throws()
    {
        var vm = BuildVm();
        // Wirft synchron (Argument-Validation vor dem await).
        Assert.Throws<ArgumentNullException>(
            () => { _ = vm.BeginConfirmationAsync(null!, 0.70, false, CancellationToken.None); });
    }

    // --- PropertyChanged ---

    [Fact]
    public void BeginConfirmation_FiresPropertyChangedForBindings()
    {
        var vm = BuildVm();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _ = vm.BeginConfirmationAsync(NewEvent(), 0.55, true, CancellationToken.None);

        Assert.Contains(nameof(CodingSessionViewModel.IsAwaitingUserDecision), changed);
        Assert.Contains(nameof(CodingSessionViewModel.PendingConfirmationEvent), changed);
        Assert.Contains(nameof(CodingSessionViewModel.PendingConfirmationConfidence), changed);
        Assert.Contains(nameof(CodingSessionViewModel.PendingConfirmationIsRed), changed);
    }

    [Fact]
    public void CompleteConfirmation_FiresPropertyChangedForBindings()
    {
        var vm = BuildVm();
        _ = vm.BeginConfirmationAsync(NewEvent(), 0.55, true, CancellationToken.None);

        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.CompleteConfirmation(CodingUserDecision.Accepted);

        Assert.Contains(nameof(CodingSessionViewModel.IsAwaitingUserDecision), changed);
        Assert.Contains(nameof(CodingSessionViewModel.PendingConfirmationEvent), changed);
    }

    // --- Service-Stubs (deckungsgleich mit FrameReadinessTests) ---

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
