using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventsListControlsTests
{
    [Fact]
    public void ApplyOrderedEvents_rebuilds_collection_and_preserves_selection()
    {
        RunOnStaThread(() =>
        {
            var late = Event("LATE");
            var early = Event("EARLY");
            var events = new ObservableCollection<CodingEvent> { late, early };
            var listBox = new ListBox { ItemsSource = events, SelectedItem = late };
            var controls = new CodingEventsListControls(listBox);

            controls.ApplyOrderedEvents(events, new[] { early, late });

            Assert.Equal(new[] { early, late }, events);
            Assert.Same(events, listBox.ItemsSource);
            Assert.Same(late, listBox.SelectedItem);
        });
    }

    [Fact]
    public void ApplyOrderedEvents_keeps_null_selection_null()
    {
        RunOnStaThread(() =>
        {
            var first = Event("FIRST");
            var events = new ObservableCollection<CodingEvent> { first };
            var listBox = new ListBox { ItemsSource = events, SelectedItem = null };
            var controls = new CodingEventsListControls(listBox);

            controls.ApplyOrderedEvents(events, new[] { first });

            Assert.Null(listBox.SelectedItem);
        });
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };

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
