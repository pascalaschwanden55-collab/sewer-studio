using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingContextFactoryTests
{
    [Fact]
    public void Erstellung_liest_keine_Quelle_und_startet_keine_Aktion()
    {
        var dependencies = new PlayerWindowCodingContextDependencies(
            new CodingSessionHost(() => throw new InvalidOperationException("Zu frueher ViewModel-Zugriff")),
            () => throw new InvalidOperationException("Zu frueher Dienst-Zugriff"),
            () => throw new InvalidOperationException("Zu frueher Import-Zugriff"),
            () => throw new InvalidOperationException("Zu frueher Kalibrierungs-Zugriff"),
            () => throw new InvalidOperationException("Zu frueher Format-Zugriff"),
            _ => throw new InvalidOperationException("Zu frueher Schnappschuss"),
            () => throw new InvalidOperationException("Zu frueher Frame-Zugriff"),
            () => throw new InvalidOperationException("Zu frueher Meter-Zugriff"),
            () => throw new InvalidOperationException("Zu frueher Zeit-Zugriff"));
        var actions = new CodingBoundaryEventWorkflowActions(
            _ => throw new InvalidOperationException("Zu fruehe Katalogsuche"),
            _ => throw new InvalidOperationException("Zu fruehe Meldung"),
            _ => throw new InvalidOperationException("Zu fruehe Bildextraktion"),
            (_, _) => throw new InvalidOperationException("Zu fruehe Fotoablage"),
            () => throw new InvalidOperationException("Zu fruehe Kalibrierung"),
            () => throw new InvalidOperationException("Zu fruehe Aktualisierung"));

        var contexts = PlayerWindowCodingContextFactory.Create(dependencies, actions);

        Assert.NotNull(contexts.Finding);
        Assert.NotNull(contexts.Analysis);
        Assert.NotNull(contexts.Boundary);
    }

    [Theory]
    [InlineData("Sitzung")]
    [InlineData("Ansicht")]
    [InlineData("Import")]
    public void Analyse_liest_nach_Quellenwechsel_das_aktuelle_Rohrende(string source)
    {
        using var state = new TestContext();
        var contexts = state.Create();

        Assert.False(contexts.Analysis.IsAfterTerminalBoundary(12.5, TimeSpan.FromSeconds(10)));

        var terminal = Event("BCE", 12.5);
        switch (source)
        {
            case "Sitzung": state.SessionService = new RecordingSessionService([terminal]); break;
            case "Ansicht": state.ViewModel = state.NewViewModel(); state.ViewModel.Events.Add(terminal); break;
            case "Import": state.ImportEvents = [terminal]; break;
        }

        Assert.True(contexts.Analysis.IsAfterTerminalBoundary(12.5, TimeSpan.FromSeconds(10)));

        state.SessionService = null;
        state.ViewModel = null;
        state.ImportEvents = [];

        Assert.False(contexts.Analysis.IsAfterTerminalBoundary(12.5, TimeSpan.FromSeconds(10)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Befunde_lesen_aktuelle_Sitzungs_und_Ansichtsereignisse(bool fromSession)
    {
        using var state = new TestContext();
        var contexts = state.Create();
        var finding = new LiveFrameFinding("Riss", 2, null, null, VsaCodeHint: "BAB");

        Assert.False(contexts.Finding.IsKnown(finding, 5.2));

        if (fromSession)
            state.SessionService = new RecordingSessionService([Event("BAB", 5)]);
        else
        {
            state.ViewModel = state.NewViewModel();
            state.ViewModel.Events.Add(Event("BAB", 5));
        }

        Assert.True(contexts.Finding.IsKnown(finding, 5.2));

        state.SessionService = null;
        state.ViewModel = null;

        Assert.False(contexts.Finding.IsKnown(finding, 5.2));
    }

    [Fact]
    public async Task Rohranfang_nutzt_spaeteren_Dienst_Import_und_Bildaktionen()
    {
        using var state = new TestContext();
        var contexts = state.Create();

        Assert.False(await contexts.Boundary.EnsureStartAsync(1.25, [9]));
        Assert.Empty(state.Actions);

        var service = new RecordingSessionService([]);
        state.SessionService = service;
        state.ViewModel = state.NewViewModel();
        state.ImportEvents = [Event("BCD", 2.5)];
        state.FirstCleanFrameSeconds = 4.5;

        Assert.True(await contexts.Boundary.EnsureStartAsync(1.25, [9]));
        var added = Assert.Single(service.Events);
        Assert.Equal("BCD", added.Entry.Code);
        Assert.Equal(2.5, added.Entry.MeterStart);
        Assert.Equal(4.5, state.ExtractedSeconds);
        Assert.Same(state.FrameBytes, state.AttachedBytes);
        Assert.Equal(new[] { "Extraktion", "Foto", "Kalibrierung" }, state.Actions);

        state.Actions.Clear();

        Assert.False(await contexts.Boundary.EnsureStartAsync(1.25, [9]));
        Assert.Single(service.Events);
        Assert.Empty(state.Actions);
    }


    private static CodingEvent Event(string code, double meter)
        => new()
        {
            Entry = new ProtocolEntry { Code = code, MeterStart = meter },
            MeterAtCapture = meter
        };

    private sealed class TestContext : IDisposable
    {
        private readonly List<CodingSessionViewModel> _viewModels = [];
        public RecordingSessionService? SessionService { get; set; }
        public CodingSessionViewModel? ViewModel { get; set; }
        public IReadOnlyList<CodingEvent> ImportEvents { get; set; } = [];
        public double? FirstCleanFrameSeconds { get; set; }
        public double? ExtractedSeconds { get; private set; }
        public byte[] FrameBytes { get; } = [1, 2, 3];
        public byte[]? AttachedBytes { get; private set; }
        public List<string> Actions { get; } = [];

        public CodingSessionViewModel NewViewModel()
        {
            var viewModel = new CodingSessionViewModel(
                SessionService ?? new RecordingSessionService([]),
                new OverlayToolService());
            _viewModels.Add(viewModel);
            return viewModel;
        }

        public PlayerWindowCodingContexts Create()
            => PlayerWindowCodingContextFactory.Create(
                new PlayerWindowCodingContextDependencies(
                    new CodingSessionHost(() => ViewModel),
                    () => SessionService,
                    () => ImportEvents,
                    () => null,
                    () => 16.0 / 9,
                    _ => false,
                    () => FirstCleanFrameSeconds,
                    () => null,
                    () => TimeSpan.Zero),
                new CodingBoundaryEventWorkflowActions(
                    _ => null,
                    _ => { },
                    seconds =>
                    {
                        ExtractedSeconds = seconds;
                        Actions.Add("Extraktion");
                        return Task.FromResult<byte[]?>(FrameBytes);
                    },
                    (_, bytes) => { AttachedBytes = bytes; Actions.Add("Foto"); },
                    () => Actions.Add("Kalibrierung"),
                    () => Actions.Add("Aktualisierung")));

        public void Dispose()
        {
            foreach (var viewModel in _viewModels)
                viewModel.Dispose();
        }
    }

    private sealed class RecordingSessionService(List<CodingEvent> events) : ICodingSessionService
    {
        public CodingSession? ActiveSession { get; } = new() { Events = events };
        public IReadOnlyList<CodingEvent> Events => ActiveSession!.Events;
        public double CurrentMeter => 0;
        public double EndMeter => 20;
        public double ProgressPercent => 0;
        public event EventHandler<CodingSessionState>? StateChanged { add { } remove { } }
        public event EventHandler<double>? MeterChanged { add { } remove { } }
        public event EventHandler<CodingEvent>? EventAdded;
        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => throw new NotSupportedException();
        public void PauseSession() => throw new NotSupportedException();
        public void ResumeSession() => throw new NotSupportedException();
        public void SetWaitingForInput() => throw new NotSupportedException();
        public void AbortSession(string reason) => throw new NotSupportedException();
        public ProtocolDocument CompleteSession() => throw new NotSupportedException();
        public void MoveNext(double stepSizeM = 0.5) => throw new NotSupportedException();
        public void MovePrevious(double stepSizeM = 0.5) => throw new NotSupportedException();
        public void MoveToMeter(double meter) => throw new NotSupportedException();
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null)
        {
            var codingEvent = new CodingEvent { Entry = entry, Overlay = overlay };
            ActiveSession!.Events.Add(codingEvent);
            EventAdded?.Invoke(this, codingEvent);
            return codingEvent;
        }
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) => throw new NotSupportedException();
        public void RemoveEvent(Guid eventId) => throw new NotSupportedException();
        public Task IndexConfirmedSampleAsync(TrainingSample sample, CancellationToken ct = default) => throw new NotSupportedException();
    }
}

