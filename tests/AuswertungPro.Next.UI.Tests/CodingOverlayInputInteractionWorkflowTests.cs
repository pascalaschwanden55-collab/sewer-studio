using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayInputInteractionWorkflowTests
{
    [Fact]
    public void Run_returns_callback_result_between_suspend_and_resume()
    {
        var calls = new List<string>();

        var result = CodingOverlayInputInteractionWorkflow.Run(
            new CodingOverlayInputInteractionWorkflowActions(
                Suspend: () => calls.Add("suspend"),
                Resume: () => calls.Add("resume")),
            () =>
            {
                calls.Add("callback");
                return 42;
            });

        Assert.Equal(42, result);
        Assert.Equal(["suspend", "callback", "resume"], calls);
    }

    [Fact]
    public void Run_resumes_when_callback_throws()
    {
        var calls = new List<string>();

        var error = Assert.Throws<InvalidOperationException>(() =>
            CodingOverlayInputInteractionWorkflow.Run(
                new CodingOverlayInputInteractionWorkflowActions(
                    Suspend: () => calls.Add("suspend"),
                    Resume: () => calls.Add("resume")),
                int () =>
                {
                    calls.Add("callback");
                    throw new InvalidOperationException("dialog failed");
                }));

        Assert.Equal("dialog failed", error.Message);
        Assert.Equal(["suspend", "callback", "resume"], calls);
    }

    [Fact]
    public async Task RunAsync_resumes_after_awaited_callback()
    {
        var calls = new List<string>();

        await CodingOverlayInputInteractionWorkflow.RunAsync(
            new CodingOverlayInputInteractionWorkflowActions(
                Suspend: () => calls.Add("suspend"),
                Resume: () => calls.Add("resume")),
            async () =>
            {
                calls.Add("callback-start");
                await Task.Yield();
                calls.Add("callback-end");
            });

        Assert.Equal(["suspend", "callback-start", "callback-end", "resume"], calls);
    }
}
