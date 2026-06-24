using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingInlineDefectSelectionResult(CodingEvent? SelectedEvent);

public static class CodingInlineDefectSelectionWorkflow
{
    public static CodingInlineDefectSelectionResult Apply(
        object? selectedItem,
        Action<CodingEvent?> setSelectedDefect)
    {
        ArgumentNullException.ThrowIfNull(setSelectedDefect);

        var selectedEvent = selectedItem as CodingEvent;
        setSelectedDefect(selectedEvent);
        return new CodingInlineDefectSelectionResult(selectedEvent);
    }
}
