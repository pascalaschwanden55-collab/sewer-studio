using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.SelfImproving;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingSessionViewModelFeedbackTests
{
    [Fact]
    public async Task CodingFeedbackRecorder_writes_validation_log_entry()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sewer-feedback-{Guid.NewGuid():N}.sqlite");
        try
        {
            var recorder = new CodingFeedbackRecorder(dbPath);
            var ev = MakeAiEvent("BBA", "BBA");
            ev.AiContext!.Decision = CodingUserDecision.Accepted;

            await recorder.RecordDecisionAsync(ev, "H-400");

            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SuggestedCode, FinalCode, WasCorrect FROM ValidationLog";
            using var reader = cmd.ExecuteReader();

            Assert.True(reader.Read());
            Assert.Equal("BBA", reader.GetString(0));
            Assert.Equal("BBA", reader.GetString(1));
            Assert.Equal(1, reader.GetInt32(2));
            Assert.False(reader.Read());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { }
            try { if (File.Exists(dbPath + "-wal")) File.Delete(dbPath + "-wal"); } catch { }
            try { if (File.Exists(dbPath + "-shm")) File.Delete(dbPath + "-shm"); } catch { }
        }
    }

    [Fact]
    public void AcceptDefect_records_feedback_for_ai_event()
    {
        var recorder = new RecordingFeedbackRecorder();
        using var vm = new CodingSessionViewModel(
            new FakeCodingSessionService(),
            new FakeOverlayToolService(),
            recorder);
        vm.HaltungName = "H-100";
        vm.SelectedDefect = MakeAiEvent("BBA", "BBA");

        vm.AcceptDefectCommand.Execute(null);

        Assert.Single(recorder.Records);
        Assert.Same(vm.SelectedDefect, recorder.Records[0].Event);
        Assert.Equal("H-100", recorder.Records[0].CaseId);
        Assert.Equal(CodingUserDecision.Accepted, vm.SelectedDefect!.AiContext!.Decision);
    }

    [Fact]
    public void RejectDefect_records_feedback_for_ai_event()
    {
        var recorder = new RecordingFeedbackRecorder();
        using var vm = new CodingSessionViewModel(
            new FakeCodingSessionService(),
            new FakeOverlayToolService(),
            recorder);
        vm.HaltungName = "H-200";
        vm.SelectedDefect = MakeAiEvent("BBA", "BBA");

        vm.RejectDefectCommand.Execute(null);

        Assert.Single(recorder.Records);
        Assert.Same(vm.SelectedDefect, recorder.Records[0].Event);
        Assert.Equal("H-200", recorder.Records[0].CaseId);
        Assert.Equal(CodingUserDecision.Rejected, vm.SelectedDefect!.AiContext!.Decision);
    }

    [Fact]
    public void AcceptDefect_does_not_record_feedback_for_manual_event()
    {
        var recorder = new RecordingFeedbackRecorder();
        using var vm = new CodingSessionViewModel(
            new FakeCodingSessionService(),
            new FakeOverlayToolService(),
            recorder);
        vm.HaltungName = "H-300";
        vm.SelectedDefect = new CodingEvent
        {
            Entry = new ProtocolEntry
            {
                Code = "BBA",
                Beschreibung = "Manuell codiert",
                MeterStart = 1.2,
                Source = ProtocolEntrySource.Manual
            },
            MeterAtCapture = 1.2
        };

        vm.AcceptDefectCommand.Execute(null);

        Assert.Empty(recorder.Records);
        Assert.Equal(CodingUserDecision.Accepted, vm.SelectedDefect!.AiContext!.Decision);
    }

    private static CodingEvent MakeAiEvent(string suggestedCode, string finalCode)
        => new()
        {
            Entry = new ProtocolEntry
            {
                Code = finalCode,
                Beschreibung = "KI-Vorschlag",
                MeterStart = 2.4,
                Source = ProtocolEntrySource.Ai
            },
            MeterAtCapture = 2.4,
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = suggestedCode,
                Confidence = 0.72,
                Reason = "Test"
            }
        };

    private sealed class RecordingFeedbackRecorder : ICodingFeedbackRecorder
    {
        public List<(CodingEvent Event, string CaseId)> Records { get; } = new();

        public Task RecordDecisionAsync(CodingEvent ev, string caseId, CancellationToken ct = default)
        {
            Records.Add((ev, caseId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCodingSessionService : ICodingSessionService
    {
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => Array.Empty<CodingEvent>();

        public event EventHandler<CodingSessionState>? StateChanged;
        public event EventHandler<double>? MeterChanged;
        public event EventHandler<CodingEvent>? EventAdded;

        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => new();
        public void PauseSession() { }
        public void ResumeSession() { }
        public void SetWaitingForInput() { }
        public void AbortSession(string reason) { }
        public ProtocolDocument CompleteSession() => new();
        public void MoveNext(double stepSizeM = 0.5) { }
        public void MovePrevious(double stepSizeM = 0.5) { }
        public void MoveToMeter(double meter) { }
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => new() { Entry = entry, Overlay = overlay };
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) { }

        public void RaiseState(CodingSessionState state) => StateChanged?.Invoke(this, state);
        public void RaiseMeter(double meter) => MeterChanged?.Invoke(this, meter);
        public void RaiseEvent(CodingEvent ev) => EventAdded?.Invoke(this, ev);
    }

    private sealed class FakeOverlayToolService : IOverlayToolService
    {
        public OverlayToolType ActiveTool { get; set; }
        public LevelMode ActiveLevelMode { get; set; }
        public bool PipeBendSnapEnabled { get; set; }
        public PipeCalibration? Calibration { get; private set; }
        public bool IsCalibrated => Calibration?.IsCalibrated == true;
        public bool IsDrawing => false;
        public bool IsMultiPointTool => false;
        public int RequiredPointCount => 0;
        public int DrawPointCount => 0;
        public IReadOnlyList<NormalizedPoint> DrawPoints => Array.Empty<NormalizedPoint>();
        public NormalizedPoint? DrawStartPoint => null;
        public NormalizedPoint? DrawCurrentPoint => null;
        public OverlayGeometry? PreviewGeometry => null;

        public event EventHandler<OverlayToolType>? ToolChanged;

        public void SetCalibration(PipeCalibration calibration) => Calibration = calibration;
        public void BeginDraw(NormalizedPoint startPoint) { }
        public void UpdateDraw(NormalizedPoint currentPoint) { }
        public OverlayGeometry? EndDraw() => null;
        public void CancelDraw() { }
        public bool AddDrawPoint(NormalizedPoint point) => false;
        public double PixelToMm(double normalizedPixels, double frameWidthPx) => 0;
        public double PointToClockHour(NormalizedPoint point) => 0;
        public OverlayGeometry? BuildLevelGeometryFromSlider(double fillPercent, LevelMode mode) => null;
        public void ResizePipeCircle(double deltaNormalized) { }
        public void MovePipeCircle(NormalizedPoint newCenter) { }

        public void RaiseTool(OverlayToolType tool) => ToolChanged?.Invoke(this, tool);
    }
}
