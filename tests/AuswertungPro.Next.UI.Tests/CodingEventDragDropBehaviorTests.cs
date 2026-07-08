using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AuswertungPro.Next.UI.Behaviors;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventDragDropBehaviorTests
{
    // Regression: InputHitTest kann einen Text-Run (ContentElement) liefern. Frueher rief die
    // Aufloesung VisualTreeHelper.GetParent direkt darauf -> "Run ist kein Visual oder Visual3D"
    // beim ersten Ziehen einer Kachel. ResolveItemData muss ContentElemente vertragen.
    [Fact]
    public void ResolveItemData_wirft_nicht_bei_content_element_run()
    {
        RunOnStaThread(() =>
        {
            var run = new Run("BCCYA @0.30m");
            var textBlock = new TextBlock();
            textBlock.Inlines.Add(run);

            // Darf NICHT werfen. Ohne gerendertes ListBoxItem in der Ahnenkette -> null.
            var result = CodingEventDragDropBehavior.ResolveItemData(run);

            Assert.Null(result);
        });
    }

    [Fact]
    public void ResolveItemData_liefert_datacontext_des_listboxitem()
    {
        RunOnStaThread(() =>
        {
            var payload = new object();
            var item = new ListBoxItem { DataContext = payload };

            // Terminalfall: getroffenes Element IST das ListBoxItem -> dessen DataContext.
            var result = CodingEventDragDropBehavior.ResolveItemData(item);

            Assert.Same(payload, result);
        });
    }

    [Fact]
    public void ResolveItemData_liefert_null_bei_null()
        => Assert.Null(CodingEventDragDropBehavior.ResolveItemData(null));

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
