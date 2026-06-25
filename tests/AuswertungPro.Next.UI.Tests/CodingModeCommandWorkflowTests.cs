using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeCommandWorkflowTests
{
    [Fact]
    public void Execute_shows_missing_haltung_without_entering_coding_mode()
    {
        var calls = new List<string>();

        var result = CodingModeCommandWorkflow.Execute(
            new CodingModeCommandRequest(HasHaltungRecord: false),
            Actions(calls));

        Assert.Equal(CodingModeCommandOutcome.MissingHaltung, result.Outcome);
        Assert.Equal(["missing"], calls);
    }

    [Fact]
    public void Execute_enters_coding_mode_when_haltung_exists()
    {
        var calls = new List<string>();

        var result = CodingModeCommandWorkflow.Execute(
            new CodingModeCommandRequest(HasHaltungRecord: true),
            Actions(calls));

        Assert.Equal(CodingModeCommandOutcome.Entered, result.Outcome);
        Assert.Equal(["enter"], calls);
    }

    private static CodingModeCommandActions Actions(List<string> calls)
        => new(
            ShowMissingHaltung: () => calls.Add("missing"),
            EnterCodingMode: () => calls.Add("enter"));
}
