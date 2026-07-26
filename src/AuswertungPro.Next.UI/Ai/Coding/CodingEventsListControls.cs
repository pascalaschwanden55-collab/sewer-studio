using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingEventsListControls
{
    private readonly ListBox _eventsList;

    public CodingEventsListControls(ListBox eventsList)
    {
        _eventsList = eventsList;
    }

    public void ApplyOrderedEvents(
        ObservableCollection<CodingEvent> events,
        IReadOnlyList<CodingEvent> orderedEvents)
    {
        var selected = _eventsList.SelectedItem;

        events.Clear();
        foreach (var ev in orderedEvents)
            events.Add(ev);

        _eventsList.ItemsSource = null;
        _eventsList.ItemsSource = events;

        if (selected != null)
            _eventsList.SelectedItem = selected;
    }

    public void SetItemsSource(IEnumerable? events)
    {
        _eventsList.ItemsSource = events;
    }

    public void SelectEvent(CodingEvent codingEvent)
    {
        _eventsList.SelectedItem = codingEvent;
    }
}
