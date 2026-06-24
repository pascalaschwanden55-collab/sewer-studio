using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAnalyzeButtonControlsTests
{
    [Fact]
    public void SetEnabled_updates_analyze_button_enabled_state()
    {
        RunOnStaThread(() =>
        {
            var button = new Button { IsEnabled = true };
            var setEnabled = FindSetEnabledMethod();
            Assert.NotNull(setEnabled);

            setEnabled.Invoke(null, [button, false]);

            Assert.False(button.IsEnabled);

            setEnabled.Invoke(null, [button, true]);

            Assert.True(button.IsEnabled);
        });
    }

    private static MethodInfo? FindSetEnabledMethod()
        => typeof(CodingModeChromeControls).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingAnalyzeButtonControls")
            ?.GetMethod(
                "SetEnabled",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(Button), typeof(bool)],
                modifiers: null);

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
}
