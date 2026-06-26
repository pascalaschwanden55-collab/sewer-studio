using System;
using System.Collections;
using System.Windows.Controls;
using System.Windows.Documents;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingImportReferenceControls
{
    public static void SetCount(Run countRun, int count)
    {
        ArgumentNullException.ThrowIfNull(countRun);

        countRun.Text = count.ToString();
    }

    public static void SetItemsSource(ListBox eventsList, IEnumerable? events)
    {
        ArgumentNullException.ThrowIfNull(eventsList);

        eventsList.ItemsSource = events;
    }

    public static void ClearItemsSource(ListBox eventsList)
    {
        ArgumentNullException.ThrowIfNull(eventsList);

        eventsList.ItemsSource = null;
    }
}
