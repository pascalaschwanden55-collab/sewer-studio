using System.Collections.ObjectModel;

namespace AuswertungPro.Next.UI.Collections;

public static class ObservableCollectionOrderController
{
    public static bool CanMoveByOffset<T>(ObservableCollection<T> collection, T? item, int offset)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (item is null || offset == 0)
            return false;

        var oldIndex = collection.IndexOf(item);
        if (oldIndex < 0)
            return false;

        var newIndex = oldIndex + offset;
        return newIndex >= 0 && newIndex < collection.Count;
    }

    public static bool TryMoveByOffset<T>(ObservableCollection<T> collection, T? item, int offset)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (item is null || offset == 0)
            return false;

        var oldIndex = collection.IndexOf(item);
        if (oldIndex < 0)
            return false;

        var newIndex = oldIndex + offset;
        if (newIndex < 0 || newIndex >= collection.Count)
            return false;

        collection.Move(oldIndex, newIndex);
        return true;
    }

    public static void Reorder<T>(ObservableCollection<T> collection, IReadOnlyList<T> ordered)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(ordered);

        for (var targetIndex = 0; targetIndex < ordered.Count && targetIndex < collection.Count; targetIndex++)
        {
            var desired = ordered[targetIndex];
            if (ReferenceEquals(collection[targetIndex], desired))
                continue;

            var currentIndex = collection.IndexOf(desired);
            if (currentIndex >= 0 && currentIndex != targetIndex)
                collection.Move(currentIndex, targetIndex);
        }
    }
}
