using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSelectedCodeControlsTests
{
    [Fact]
    public void Clear_empties_selected_code_text()
    {
        RunOnStaThread(() =>
        {
            var text = new TextBlock { Text = "BCA - Anschluss" };

            CodingSelectedCodeControls.Clear(text);

            Assert.Equal("", text.Text);
        });
    }

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
