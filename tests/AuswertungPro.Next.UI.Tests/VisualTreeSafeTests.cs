using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using AuswertungPro.Next.UI.Behaviors;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VisualTreeSafeTests
{
    // Regression: e.OriginalSource / InputHitTest kann ein ContentElement (Text-Run) sein.
    // VisualTreeHelper.GetParent wuerfe darauf "... ist kein Visual oder Visual3D".
    [Fact]
    public void GetParentSafe_wirft_nicht_bei_run_und_liefert_logischen_parent()
    {
        RunOnStaThread(() =>
        {
            var run = new Run("BAB Riss");
            var textBlock = new TextBlock();
            textBlock.Inlines.Add(run);

            // ContentElement -> LogicalTree: der logische Elternteil eines Runs ist sein TextBlock.
            var parent = VisualTreeSafe.GetParentSafe(run);

            Assert.Same(textBlock, parent);
        });
    }

    [Fact]
    public void FindAncestor_wirft_nicht_bei_run_ohne_treffer()
    {
        RunOnStaThread(() =>
        {
            var run = new Run("x");
            var textBlock = new TextBlock();
            textBlock.Inlines.Add(run);

            // Darf NICHT werfen; ohne ListBoxItem-Vorfahr -> null.
            var found = VisualTreeSafe.FindAncestor<ListBoxItem>(run);

            Assert.Null(found);
        });
    }

    [Fact]
    public void GetParentSafe_ist_null_bei_null()
        => Assert.Null(VisualTreeSafe.GetParentSafe(null));

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
