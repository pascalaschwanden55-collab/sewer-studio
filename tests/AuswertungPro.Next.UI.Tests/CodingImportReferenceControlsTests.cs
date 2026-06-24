using System.Reflection;
using System.Threading;
using System.Windows.Documents;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportReferenceControlsTests
{
    [Fact]
    public void SetCount_writes_count_text_to_run()
    {
        RunOnStaThread(() =>
        {
            var countRun = new Run("alt");
            var setCount = FindSetCountMethod();
            Assert.NotNull(setCount);

            setCount.Invoke(null, [countRun, 12]);

            Assert.Equal("12", countRun.Text);
        });
    }

    private static MethodInfo? FindSetCountMethod()
        => typeof(CodingImportReferenceTransfer).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingImportReferenceControls")
            ?.GetMethod(
                "SetCount",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(Run), typeof(int)],
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
