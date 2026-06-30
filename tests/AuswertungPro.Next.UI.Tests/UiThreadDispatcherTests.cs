using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class UiThreadDispatcherTests
{
    [Fact]
    public void Run_fuehrt_action_ohne_wpf_dispatcher_direkt_aus()
    {
        var calls = new List<string>();

        UiThreadDispatcher.Run(() => calls.Add("run"));

        Assert.Equal(new[] { "run" }, calls);
    }

    [Fact]
    public void Run_erfordert_action()
    {
        Assert.Throws<ArgumentNullException>(() => UiThreadDispatcher.Run(null!));
    }
}
