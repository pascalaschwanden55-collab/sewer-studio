using System.Collections.ObjectModel;

namespace AuswertungPro.Next.UI.Collections;

public static class ObservableCollectionContentController
{
    public static void ReplaceWith<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(items);

        collection.Clear();
        foreach (var item in items)
            collection.Add(item);
    }

    public static void Append<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
            collection.Add(item);
    }
}
