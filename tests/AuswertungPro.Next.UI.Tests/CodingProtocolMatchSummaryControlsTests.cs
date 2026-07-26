using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolMatchSummaryControlsTests
{
    [Fact]
    public void Apply_sets_summary_text_and_accept_button_state()
    {
        RunOnStaThread(() =>
        {
            var summaryText = new TextBlock { Text = "alt" };
            var acceptButton = new Button { IsEnabled = true };
            var apply = FindApplyMethod();
            Assert.NotNull(apply);

            apply.Invoke(null, [summaryText, acceptButton, null]);

            Assert.Equal("Abgleich: noch nicht ausgefuehrt", summaryText.Text);
            Assert.False(acceptButton.IsEnabled);
        });
    }

    private static MethodInfo? FindApplyMethod()
        => typeof(CodingProtocolMatchSummaryFormatter).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingProtocolMatchSummaryControls")
            ?.GetMethod(
                "Apply",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(TextBlock), typeof(Button), typeof(CodingMatchRouting)],
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
