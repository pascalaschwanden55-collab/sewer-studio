using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Die Ziehoperationen des Anpassen-Modus, gerechnet auf der angezeigten Gruppenliste:
/// eine Karte innerhalb ihrer Spalte, eine Karte in eine andere Spalte, eine ganze Spalte.
///
/// Rein rechnend, ohne Oberflaeche. Was dabei herauskommt, wird ueber
/// <see cref="RecordDetailLayoutApplier.Capture"/> zum gespeicherten Layout.
/// </summary>
public static class RecordDetailDragOperations
{
    /// <summary>Findet eine Karte: in welcher Spalte und an welcher Stelle.</summary>
    public static bool TryLocateField(
        IReadOnlyList<RecordDetailGroup> groups,
        RecordDetailItem item,
        out string groupTitle,
        out int index)
    {
        ArgumentNullException.ThrowIfNull(groups);

        groupTitle = string.Empty;
        index = -1;
        if (item is null)
            return false;

        foreach (var group in groups)
        {
            for (var i = 0; i < group.Items.Count; i++)
            {
                if (!ReferenceEquals(group.Items[i], item))
                    continue;

                groupTitle = group.Title;
                index = i;
                return true;
            }
        }

        return false;
    }

    /// <summary>Findet die Stelle einer Spalte.</summary>
    public static bool TryLocateColumn(
        IReadOnlyList<RecordDetailGroup> groups,
        RecordDetailGroup group,
        out int index)
    {
        ArgumentNullException.ThrowIfNull(groups);

        index = -1;
        if (group is null)
            return false;

        for (var i = 0; i < groups.Count; i++)
        {
            if (!ReferenceEquals(groups[i], group))
                continue;

            index = i;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Verschiebt eine Karte an eine andere Stelle — innerhalb ihrer Spalte oder in eine
    /// andere. Liefert <c>null</c>, wenn sich dadurch nichts aendert oder die Angaben
    /// nicht passen; dann darf weder neu gezeichnet noch gespeichert werden.
    /// </summary>
    public static IReadOnlyList<RecordDetailGroup>? MoveField(
        IReadOnlyList<RecordDetailGroup> groups,
        string fromGroupTitle,
        int fromIndex,
        string toGroupTitle,
        int toIndex)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var fromColumn = IndexOfTitle(groups, fromGroupTitle);
        var toColumn = IndexOfTitle(groups, toGroupTitle);
        if (fromColumn < 0 || toColumn < 0)
            return null;

        var source = groups[fromColumn].Items;
        if (fromIndex < 0 || fromIndex >= source.Count)
            return null;

        if (fromColumn == toColumn)
        {
            var reordered = RecordDetailOrderRanking.Move(source, fromIndex, toIndex);
            if (reordered is null)
                return null;

            var sameResult = new List<RecordDetailGroup>(groups);
            sameResult[fromColumn] = groups[fromColumn] with { Items = reordered };
            return sameResult;
        }

        var target = groups[toColumn].Items;
        // In einer fremden Spalte darf hinter das letzte Element eingefuegt werden.
        if (toIndex < 0 || toIndex > target.Count)
            return null;

        var moved = source[fromIndex];
        var neueQuelle = new List<RecordDetailItem>(source);
        neueQuelle.RemoveAt(fromIndex);

        var neuesZiel = new List<RecordDetailItem>(target);
        neuesZiel.Insert(toIndex, moved);

        var result = new List<RecordDetailGroup>(groups);
        result[fromColumn] = groups[fromColumn] with { Items = neueQuelle };
        result[toColumn] = groups[toColumn] with { Items = neuesZiel };
        return result;
    }

    /// <summary>Verschiebt eine ganze Spalte mitsamt ihren Karten.</summary>
    public static IReadOnlyList<RecordDetailGroup>? MoveColumn(
        IReadOnlyList<RecordDetailGroup> groups,
        int fromIndex,
        int toIndex)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return RecordDetailOrderRanking.Move(groups, fromIndex, toIndex);
    }

    /// <summary>
    /// Rechnet aus Aufnahme- und Zielkarte die Zielstelle aus.
    /// <paramref name="insertAfter"/> heisst: unterhalb der Zielkarte abgelegt.
    ///
    /// Innerhalb derselben Spalte wird die Karte erst entnommen, alles dahinter rutscht
    /// auf. In einer fremden Spalte wird nichts entnommen — dort ist die Stelle hinter
    /// dem letzten Element eine gueltige Zielstelle, und auch eine leere Spalte nimmt
    /// eine Karte auf.
    /// </summary>
    public static int ResolveDropTarget(int fromIndex, int targetIndex, bool insertAfter, int count, bool sameColumn)
    {
        if (sameColumn)
            return RecordDetailOrderRanking.ResolveDropTarget(fromIndex, targetIndex, insertAfter, count);

        if (count <= 0)
            return 0;
        if (targetIndex < 0 || targetIndex >= count)
            return -1;

        return insertAfter ? targetIndex + 1 : targetIndex;
    }

    private static int IndexOfTitle(IReadOnlyList<RecordDetailGroup> groups, string title)
    {
        for (var i = 0; i < groups.Count; i++)
        {
            if (string.Equals(groups[i].Title, title, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }
}
