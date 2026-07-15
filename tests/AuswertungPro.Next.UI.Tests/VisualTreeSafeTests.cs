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
    public void FindAncestor_findet_logischen_und_visuellen_Vorfahren()
    {
        RunOnStaThread(() =>
        {
            var run = new Run("BAB");
            var textBlock = new TextBlock();
            textBlock.Inlines.Add(run);
            var border = new Border { Child = textBlock };

            Assert.Same(textBlock, VisualTreeSafe.FindAncestor<TextBlock>(run));
            Assert.Same(border, VisualTreeSafe.FindAncestor<Border>(run));
        });
    }

    [Fact]
    public void GetParentSafe_ist_null_bei_null()
        => Assert.Null(VisualTreeSafe.GetParentSafe(null));

    [Fact]
    public void FindNamedDescendant_findet_verschachteltes_benanntes_element()
    {
        RunOnStaThread(() =>
        {
            var expected = new TextBlock { Name = "Treffer" };
            var root = new Border
            {
                Child = new Grid
                {
                    Children = { new TextBlock { Name = "Andere" }, expected }
                }
            };

            var found = VisualTreeSafe.FindNamedDescendant<TextBlock>(root, "Treffer");

            Assert.Same(expected, found);
        });
    }

    [Fact]
    public void FindNamedDescendant_ist_fuer_null_und_content_element_sicher()
    {
        RunOnStaThread(() =>
        {
            Assert.Null(VisualTreeSafe.FindNamedDescendant<TextBlock>(null, "Treffer"));
            Assert.Null(VisualTreeSafe.FindNamedDescendant<TextBlock>(new Run("Text"), "Treffer"));
        });
    }

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
