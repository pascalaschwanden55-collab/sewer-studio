using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerDispatchWorkflowTests
{
    [Fact]
    public void DispatchPropertyChanged_on_ui_thread_applies_immediately()
    {
        var calls = new List<string>();

        VsaCodeExplorerDispatchWorkflow.DispatchPropertyChanged(
            isOnUiThread: true,
            apply: () => calls.Add("apply"),
            postToUi: action =>
            {
                calls.Add("post");
                action();
            });

        Assert.Equal(["apply"], calls);
    }

    [Fact]
    public void DispatchPropertyChanged_from_background_thread_posts_without_running_immediately()
    {
        var calls = new List<string>();
        Action? posted = null;

        VsaCodeExplorerDispatchWorkflow.DispatchPropertyChanged(
            isOnUiThread: false,
            apply: () => calls.Add("apply"),
            postToUi: action =>
            {
                calls.Add("post");
                posted = action;
            });

        Assert.Equal(["post"], calls);

        posted!.Invoke();

        Assert.Equal(["post", "apply"], calls);
    }

    [Fact]
    public void ScheduleColumnRender_posts_render_action()
    {
        var calls = new List<string>();
        Action? posted = null;

        VsaCodeExplorerDispatchWorkflow.ScheduleColumnRender(
            render: () => calls.Add("render"),
            postToUi: action =>
            {
                calls.Add("post");
                posted = action;
            });

        Assert.Equal(["post"], calls);

        posted!.Invoke();

        Assert.Equal(["post", "render"], calls);
    }
}
