using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingCaseSelectionControllerTests
{
    [Fact]
    public void Select_keeps_selected_case_with_protocol()
    {
        var selected = Case("H-001", protocolPath: @"C:\p\h-001.pdf");

        var result = SelfTrainingCaseSelectionController.Select(
            selected,
            cases: Array.Empty<TrainingCase>(),
            existingSamples: new[] { Sample("H-001") });

        Assert.False(result.ShouldStop);
        Assert.Same(selected, result.Case);
        Assert.Null(result.StatusText);
    }

    [Fact]
    public void Select_stops_when_selected_case_has_no_protocol()
    {
        var selected = Case("H-001", protocolPath: "");

        var result = SelfTrainingCaseSelectionController.Select(
            selected,
            cases: Array.Empty<TrainingCase>(),
            existingSamples: Array.Empty<TrainingSample>());

        Assert.True(result.ShouldStop);
        Assert.Same(selected, result.Case);
        Assert.Equal("Der ausgewaehlte Fall hat kein Protokoll (PDF).", result.StatusText);
    }

    [Fact]
    public void Select_returns_first_unprocessed_case_with_protocol()
    {
        var noProtocol = Case("H-000", protocolPath: "");
        var processed = Case("H-001", protocolPath: @"C:\p\h-001.pdf");
        var unprocessed = Case("H-002", protocolPath: @"C:\p\h-002.pdf");

        var result = SelfTrainingCaseSelectionController.Select(
            selectedCase: null,
            cases: new[] { noProtocol, processed, unprocessed },
            existingSamples: new[] { Sample("h-001") });

        Assert.False(result.ShouldStop);
        Assert.Same(unprocessed, result.Case);
        Assert.Null(result.StatusText);
    }

    [Fact]
    public void Select_stops_when_all_protocol_cases_are_processed()
    {
        var result = SelfTrainingCaseSelectionController.Select(
            selectedCase: null,
            cases: new[]
            {
                Case("H-001", protocolPath: @"C:\p\h-001.pdf"),
                Case("H-002", protocolPath: @"C:\p\h-002.pdf")
            },
            existingSamples: new[] { Sample("H-001"), Sample("H-002") });

        Assert.True(result.ShouldStop);
        Assert.Null(result.Case);
        Assert.Equal("Alle 2 Faelle bereits verarbeitet. Waehle manuell fuer erneutes Training.", result.StatusText);
    }

    [Fact]
    public void Select_stops_when_no_case_has_protocol()
    {
        var result = SelfTrainingCaseSelectionController.Select(
            selectedCase: null,
            cases: new[] { Case("H-001", protocolPath: "") },
            existingSamples: Array.Empty<TrainingSample>());

        Assert.True(result.ShouldStop);
        Assert.Null(result.Case);
        Assert.Equal("Keine Faelle mit Protokoll vorhanden. Bitte zuerst Ordner waehlen und scannen.", result.StatusText);
    }

    private static TrainingCase Case(string caseId, string protocolPath)
        => new()
        {
            CaseId = caseId,
            ProtocolPath = protocolPath
        };

    private static TrainingSample Sample(string caseId)
        => new()
        {
            CaseId = caseId
        };
}
