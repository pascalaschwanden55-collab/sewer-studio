using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingCaseSelectionWorkflowControllerTests
{
    [Fact]
    public async Task RunAsync_uses_selected_case_without_loading_existing_samples()
    {
        var selected = Case("H-001", protocolPath: "selected.pdf");
        var loadSamplesCalled = false;
        string? statusText = null;
        TrainingCase? selectedByWorkflow = null;

        var result = await SelfTrainingCaseSelectionWorkflowController.RunAsync(
            selected,
            cases: [],
            loadSamplesAsync: () =>
            {
                loadSamplesCalled = true;
                return Task.FromResult(new List<TrainingSample>());
            },
            setStatus: value => statusText = value,
            setSelectedCase: value => selectedByWorkflow = value);

        Assert.False(result.ShouldStop);
        Assert.Same(selected, result.Case);
        Assert.Null(result.StatusText);
        Assert.False(loadSamplesCalled);
        Assert.Null(statusText);
        Assert.Same(selected, selectedByWorkflow);
    }

    [Fact]
    public async Task RunAsync_loads_existing_samples_only_when_no_case_is_selected()
    {
        var processed = Case("H-001", protocolPath: "processed.pdf");
        var unprocessed = Case("H-002", protocolPath: "unprocessed.pdf");
        var loadSamplesCalled = false;
        string? statusText = null;
        TrainingCase? selectedByWorkflow = null;

        var result = await SelfTrainingCaseSelectionWorkflowController.RunAsync(
            selectedCase: null,
            cases: [processed, unprocessed],
            loadSamplesAsync: () =>
            {
                loadSamplesCalled = true;
                return Task.FromResult(new List<TrainingSample> { Sample("h-001") });
            },
            setStatus: value => statusText = value,
            setSelectedCase: value => selectedByWorkflow = value);

        Assert.False(result.ShouldStop);
        Assert.Same(unprocessed, result.Case);
        Assert.Null(result.StatusText);
        Assert.True(loadSamplesCalled);
        Assert.Null(statusText);
        Assert.Same(unprocessed, selectedByWorkflow);
    }

    [Fact]
    public async Task RunAsync_stops_with_existing_selection_status()
    {
        string? statusText = null;
        TrainingCase? selectedByWorkflow = null;

        var result = await SelfTrainingCaseSelectionWorkflowController.RunAsync(
            selectedCase: null,
            cases: [Case("H-003", protocolPath: "")],
            loadSamplesAsync: () => Task.FromResult(new List<TrainingSample>()),
            setStatus: value => statusText = value,
            setSelectedCase: value => selectedByWorkflow = value);

        Assert.True(result.ShouldStop);
        Assert.Null(result.Case);
        Assert.Equal("Keine Faelle mit Protokoll vorhanden. Bitte zuerst Ordner waehlen und scannen.", result.StatusText);
        Assert.Equal("Keine Faelle mit Protokoll vorhanden. Bitte zuerst Ordner waehlen und scannen.", statusText);
        Assert.Null(selectedByWorkflow);
    }

    [Fact]
    public async Task RunAsync_stops_selected_case_without_protocol_and_keeps_status_text()
    {
        var selected = Case("H-001", protocolPath: "");
        var loadSamplesCalled = false;
        string? statusText = null;
        TrainingCase? selectedByWorkflow = null;

        var result = await SelfTrainingCaseSelectionWorkflowController.RunAsync(
            selected,
            cases: [],
            loadSamplesAsync: () =>
            {
                loadSamplesCalled = true;
                return Task.FromResult(new List<TrainingSample>());
            },
            setStatus: value => statusText = value,
            setSelectedCase: value => selectedByWorkflow = value);

        Assert.True(result.ShouldStop);
        Assert.Same(selected, result.Case);
        Assert.Equal("Der ausgewaehlte Fall hat kein Protokoll (PDF).", result.StatusText);
        Assert.Equal("Der ausgewaehlte Fall hat kein Protokoll (PDF).", statusText);
        Assert.False(loadSamplesCalled);
        Assert.Null(selectedByWorkflow);
    }

    private static TrainingCase Case(string caseId, string protocolPath)
        => new()
        {
            CaseId = caseId,
            ProtocolPath = protocolPath
        };

    private static TrainingSample Sample(string caseId)
        => new() { CaseId = caseId };
}
