using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingConfirmationControllerFactoryTests
{
    [Fact]
    public void Create_wires_shared_pending_state_and_reads_live_dependencies_on_demand()
    {
        RunOnStaThread(() =>
        {
            var calls = new List<string>();
            var pendingState = new CodingPendingConfirmationStateController();
            var sessionOwner = new CodingSessionServiceOwner();
            var sessionHost = new MutableCodingSessionHost();
            CodingEvent? persistedEvent = null;
            var trainingPersistence = new CodingTrainingPersistenceContext(
                (codingEvent, _) =>
                {
                    calls.Add("persist");
                    persistedEvent = codingEvent;
                    return Task.CompletedTask;
                },
                (_, _) => Task.CompletedTask,
                () => true,
                CreateTrainingRequest);
            var confirmationPanel = new Border { Visibility = Visibility.Collapsed };
            var confirmationPanelOwner = new CodingConfirmationPanelControlsOwner();
            confirmationPanelOwner.Initialize(
                new CodingConfirmationPanelControls(
                    confirmationPanel,
                    new Ellipse(),
                    new TextBlock(),
                    new TextBlock(),
                    new TextBlock(),
                    new TextBlock()));
            var eventsList = new ListBox();
            var currentStatusText = new TextBlock { Text = "alter Status" };
            var liveAiToggle = new CheckBox { IsChecked = false };
            var playbackHost = CreatePlaybackHost(calls);
            var statusController = new RecordingStatusController(calls);
            var controller = PlayerWindowCodingConfirmationControllerFactory.Create(
                new PlayerWindowCodingConfirmationControllerDependencies(
                    PendingState: pendingState,
                    SessionRuntimeOwner: sessionOwner,
                    SessionHost: sessionHost,
                    TrainingPersistence: trainingPersistence,
                    RefreshEvents: () => calls.Add("refresh"),
                    ConfirmationPanel: confirmationPanelOwner,
                    EventsList: new CodingEventsListControls(eventsList),
                    CurrentStatusText: currentStatusText,
                    LiveAiToggle: liveAiToggle,
                    AiRuntimeOwner: new CodingAiControllerOwner(),
                    PlaybackControlHost: playbackHost,
                    StatusController: statusController));

            Assert.Empty(calls);
            Assert.False(pendingState.HasPendingConfirmation);

            var codingEvent = new CodingEvent
            {
                Entry = new ProtocolEntry { Code = "BBA", Beschreibung = "Riss" },
                AiContext = new CodingEventAiContext
                {
                    SuggestedCode = "BBA",
                    Confidence = 0.8,
                    Reason = "KI-Vorschlag"
                }
            };
            var eventCollection = new ObservableCollection<CodingEvent> { codingEvent };
            var session = new CodingSession
            {
                State = CodingSessionState.Running,
                Events = [codingEvent]
            };
            var sessionService = new RecordingCodingSessionService(session, calls);
            sessionOwner.Set(sessionService);
            sessionHost.EventCollection = eventCollection;
            currentStatusText.Text = "spaeter Status";
            liveAiToggle.IsChecked = true;
            var gate = new QualityGateResult(
                0.8,
                TrafficLight.Yellow,
                new Dictionary<string, double>(),
                "test");

            controller.PauseAndAsk(codingEvent, gate);

            Assert.True(pendingState.HasPendingConfirmation);
            Assert.Same(codingEvent, pendingState.CodingEvent);
            Assert.Same(gate, pendingState.GateResult);
            Assert.Equal(CodingSessionState.WaitingForUserInput, session.State);
            Assert.Equal(Visibility.Visible, confirmationPanel.Visibility);
            Assert.Equal("spaeter Status", statusController.CodingStates[0].Status);
            Assert.Contains("pause:True", calls);

            var result = controller.Reject();

            Assert.True(result.Applied);
            Assert.Same(codingEvent, persistedEvent);
            Assert.Empty(eventCollection);
            Assert.Empty(session.Events);
            Assert.False(pendingState.HasPendingConfirmation);
            Assert.Equal(Visibility.Collapsed, confirmationPanel.Visibility);
            Assert.Equal(CodingSessionState.Running, session.State);
            Assert.Contains("refresh", calls);
            Assert.Contains("pause:False", calls);
            Assert.Equal(PlayerStatusColors.Success, statusController.CodingStates[^1].Color);
        });
    }

    private static CodingTrainingSamplePersistenceRequest CreateTrainingRequest()
        => new(
            "case",
            null,
            null,
            null,
            null,
            () => Task.FromResult<byte[]?>(null));

    private static PlayerPlaybackControlHost CreatePlaybackHost(ICollection<string> calls)
        => new(
            readIsPlaying: () => false,
            setPause: paused => calls.Add($"pause:{paused}"),
            play: () => { },
            stop: () => { },
            readRate: () => 1,
            setRate: _ => 0,
            readVolume: () => 100,
            setVolume: _ => { },
            readMute: () => false,
            setMute: _ => { },
            shouldStartPlayback: () => true,
            playPath: _ => { });

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }

    private sealed class RecordingStatusController(ICollection<string> calls)
        : ILiveDetectionStatusController
    {
        public List<(string Status, Color Color, string? Detail)> CodingStates { get; } = [];

        public void SetLiveDetectionBadge(string status, Color dotColor, string? stage = null)
            => throw new NotSupportedException();

        public void SetYoloStatus(string text, Color dotColor, string? model = null)
            => throw new NotSupportedException();

        public void SetCodingAiState(
            string status,
            Color dotColor,
            string? stage = null,
            bool pulse = false)
        {
            calls.Add($"status:{status}");
            CodingStates.Add((status, dotColor, stage));
        }

        public void UpdateDetectionStatus(LiveDetection result)
            => throw new NotSupportedException();
    }

    private sealed class MutableCodingSessionHost : ICodingSessionHost
    {
        public ObservableCollection<CodingEvent>? EventCollection { get; set; }
        public bool HasViewModel => EventCollection is not null;
        public bool IsRunningOrPaused => false;
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public OverlayGeometry? CurrentOverlay => null;
        public IEnumerable<CodingEvent> Events => EventCollection ?? [];
        public CodingEvent? SelectedDefect => null;
        public string? HaltungName => null;
        public string? VideoPath => null;
        public TimeSpan? CurrentVideoTime => null;
        public string SelectedCode => string.Empty;
        public string SelectedCodeDescription => string.Empty;
        public void SetCurrentVideoTime(TimeSpan videoTime) { }
        public void SelectDefect(CodingEvent? codingEvent) { }
        public void ClearSelectedDefect() { }
        public void SetCurrentOverlay(OverlayGeometry? overlay) { }
        public void ClearCurrentOverlay() { }
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

    private sealed class RecordingCodingSessionService(
        CodingSession session,
        ICollection<string> calls) : ICodingSessionService
    {
        public CodingSession? ActiveSession => session;
        public IReadOnlyList<CodingEvent> Events => session.Events;
        public double CurrentMeter => session.CurrentMeter;
        public double EndMeter => session.EndMeter;
        public double ProgressPercent => session.ProgressPercent;

        public event EventHandler<CodingSessionState>? StateChanged { add { } remove { } }
        public event EventHandler<double>? MeterChanged { add { } remove { } }
        public event EventHandler<CodingEvent>? EventAdded { add { } remove { } }

        public CodingSession StartSession(HaltungRecord haltung, string? videoPath)
            => throw new NotSupportedException();

        public void PauseSession()
            => throw new NotSupportedException();

        public void ResumeSession()
        {
            calls.Add("resume");
            session.State = CodingSessionState.Running;
        }

        public void SetWaitingForInput()
        {
            calls.Add("waiting");
            session.State = CodingSessionState.WaitingForUserInput;
        }

        public void AbortSession(string reason)
            => throw new NotSupportedException();

        public ProtocolDocument CompleteSession()
            => throw new NotSupportedException();

        public void MoveNext(double stepSizeM = 0.5)
            => throw new NotSupportedException();

        public void MovePrevious(double stepSizeM = 0.5)
            => throw new NotSupportedException();

        public void MoveToMeter(double meter)
            => throw new NotSupportedException();

        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null)
            => throw new NotSupportedException();

        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null)
            => throw new NotSupportedException();

        public void RemoveEvent(Guid eventId)
        {
            calls.Add("remove");
            session.Events.RemoveAll(codingEvent => codingEvent.EventId == eventId);
        }

        public Task IndexConfirmedSampleAsync(
            TrainingSample sample,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
