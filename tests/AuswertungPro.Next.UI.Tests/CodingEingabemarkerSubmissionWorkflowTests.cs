using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerSubmissionWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_empty_keyword_without_side_effects()
    {
        var calls = new List<string>();

        var result = await CodingEingabemarkerSubmissionWorkflow.ExecuteAsync(
            new CodingEingabemarkerSubmissionWorkflowRequest(
                RawKeyword: "  ",
                HasCodingViewModel: true,
                HasCodingSessionService: true),
            Actions(calls));

        Assert.Equal(CodingEingabemarkerSubmissionWorkflowOutcome.EmptyKeyword, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task ExecuteAsync_reports_duplicate_and_cancels_marker()
    {
        var calls = new List<string>();

        var result = await CodingEingabemarkerSubmissionWorkflow.ExecuteAsync(
            new CodingEingabemarkerSubmissionWorkflowRequest(
                RawKeyword: "  Anschluss  ",
                HasCodingViewModel: true,
                HasCodingSessionService: true),
            Actions(
                calls,
                resolveCodeHint: keyword =>
                {
                    calls.Add($"resolve:{keyword}");
                    return "BCA";
                },
                findDuplicate: code =>
                {
                    calls.Add($"duplicate:{code}");
                    return new CodingEingabemarkerDuplicateMatch(12.34);
                }));

        Assert.Equal(CodingEingabemarkerSubmissionWorkflowOutcome.Duplicate, result.Outcome);
        Assert.Equal(
            [
                "hide",
                "analyzing",
                "resolve:Anschluss",
                "duplicate:BCA",
                "duplicate-status:BCA:12.34",
                "cancel"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_adds_direct_event_when_code_hint_and_session_are_available()
    {
        var calls = new List<string>();

        var result = await CodingEingabemarkerSubmissionWorkflow.ExecuteAsync(
            new CodingEingabemarkerSubmissionWorkflowRequest(
                RawKeyword: "Riss",
                HasCodingViewModel: true,
                HasCodingSessionService: true),
            Actions(
                calls,
                resolveCodeHint: _ => "BBA",
                findDuplicate: _ => null));

        Assert.Equal(CodingEingabemarkerSubmissionWorkflowOutcome.DirectEventAdded, result.Outcome);
        Assert.Equal(["hide", "analyzing", "direct:BBA:Riss", "cancel"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_runs_ai_fallback_when_code_hint_is_missing()
    {
        var calls = new List<string>();

        var result = await CodingEingabemarkerSubmissionWorkflow.ExecuteAsync(
            new CodingEingabemarkerSubmissionWorkflowRequest(
                RawKeyword: "unbekannt",
                HasCodingViewModel: true,
                HasCodingSessionService: true),
            Actions(calls, resolveCodeHint: _ => null));

        Assert.Equal(CodingEingabemarkerSubmissionWorkflowOutcome.AiFallbackStarted, result.Outcome);
        Assert.Equal(["hide", "analyzing", "ai-status:unbekannt", "ai:unbekannt", "cancel"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_reports_error_and_cancels_marker()
    {
        var calls = new List<string>();

        var result = await CodingEingabemarkerSubmissionWorkflow.ExecuteAsync(
            new CodingEingabemarkerSubmissionWorkflowRequest(
                RawKeyword: "Riss",
                HasCodingViewModel: true,
                HasCodingSessionService: true),
            Actions(
                calls,
                addDirectEvent: (_, _) => throw new InvalidOperationException("append failed")));

        Assert.Equal(CodingEingabemarkerSubmissionWorkflowOutcome.Error, result.Outcome);
        Assert.Equal(["hide", "analyzing", "error:append failed", "cancel"], calls);
    }

    private static CodingEingabemarkerSubmissionWorkflowActions Actions(
        List<string> calls,
        Func<string, string?>? resolveCodeHint = null,
        Func<string, CodingEingabemarkerDuplicateMatch?>? findDuplicate = null,
        Action<string, string>? addDirectEvent = null)
        => new(
            HideInput: () => calls.Add("hide"),
            SetAnalyzingPhase: () => calls.Add("analyzing"),
            ResolveCodeHint: resolveCodeHint ?? (_ => "BBA"),
            FindDuplicate: findDuplicate ?? (_ => null),
            ShowDuplicateStatus: (code, meter) => calls.Add($"duplicate-status:{code}:{meter:F2}"),
            AddDirectEvent: addDirectEvent ?? ((code, keyword) => calls.Add($"direct:{code}:{keyword}")),
            ShowAiFallbackStatus: keyword => calls.Add($"ai-status:{keyword}"),
            RunAiFallbackAsync: keyword =>
            {
                calls.Add($"ai:{keyword}");
                return Task.CompletedTask;
            },
            ShowErrorStatus: message => calls.Add($"error:{message}"),
            CancelMarker: () => calls.Add("cancel"));
}
