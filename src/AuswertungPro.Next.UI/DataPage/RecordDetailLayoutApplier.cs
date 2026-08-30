using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Wendet die persoenliche Gestaltung der Detailansicht auf die vom Builder gelieferten
/// Gruppen an und liest sie von dort auch wieder aus.
///
/// Rein rechnend, ohne Oberflaeche und ohne Dateizugriff. Die fachliche Feldreihenfolge
/// (<c>FieldCatalog.ColumnOrder</c>) wird dabei nie gelesen und nie veraendert.
/// </summary>
public static class RecordDetailLayoutApplier
{
    /// <summary>
    /// Ordnet Spalten und Karten nach dem gespeicherten Layout und markiert die
    /// ausgeblendeten Felder.
    ///
    /// Ohne Layout wird die Eingabe unveraendert zurueckgegeben — das ist die
    /// Rueckfallebene auf das Verhalten ohne persoenliche Einstellung.
    ///
    /// Was das Layout nicht kennt, bleibt an seinem bisherigen Platz: eine unbekannte
    /// Spalte und ein unbekanntes Feld haengen sich an ihren jeweiligen Vorgaenger, statt
    /// ans Ende zu rutschen. Was das Layout nennt, es aber nicht mehr gibt, wird
    /// uebergangen.
    /// </summary>
    public static IReadOnlyList<RecordDetailGroup> Apply(
        IReadOnlyList<RecordDetailGroup> groups,
        RecordDetailLayout? layout)
    {
        ArgumentNullException.ThrowIfNull(groups);

        if (layout is null || layout.IsEmpty)
            return groups;

        var hidden = new HashSet<string>(layout.HiddenFields, StringComparer.Ordinal);
        ApplyHidden(groups, hidden);

        var columnByField = BuildColumnAssignment(layout);
        var regrouped = Regroup(groups, layout, columnByField);
        return SortColumns(regrouped, layout);
    }

    /// <summary>
    /// Liest den aktuellen Anzeigezustand als Layout aus — genau das wird gespeichert.
    /// Karten ohne Feldschluessel bleiben aussen vor; ohne ihn liesse sich eine Karte
    /// spaeter nicht wiederfinden.
    /// </summary>
    public static RecordDetailLayout Capture(IReadOnlyList<RecordDetailGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var columns = new List<RecordDetailLayoutColumn>(groups.Count);
        var hidden = new List<string>();

        foreach (var group in groups)
        {
            var fields = new List<string>(group.Items.Count);
            foreach (var item in group.Items)
            {
                if (string.IsNullOrEmpty(item.FieldName))
                    continue;

                fields.Add(item.FieldName);
                if (item.IsHiddenByUser)
                    hidden.Add(item.FieldName);
            }

            columns.Add(new RecordDetailLayoutColumn(group.Title, fields));
        }

        return new RecordDetailLayout(columns, hidden);
    }

    /// <summary>
    /// Setzt die Ausblendung jeder Karte auf den Stand des Layouts. Bewusst in beide
    /// Richtungen: ein frueher ausgeblendetes Feld, das im Layout nicht mehr steht, wird
    /// wieder sichtbar. <c>IsVisible</c> bleibt unberuehrt — das ist die fachliche Regel
    /// der Sanierungs-Folgefelder und keine persoenliche Einstellung.
    /// </summary>
    private static void ApplyHidden(IReadOnlyList<RecordDetailGroup> groups, HashSet<string> hidden)
    {
        foreach (var group in groups)
        {
            foreach (var item in group.Items)
                item.IsHiddenByUser = !string.IsNullOrEmpty(item.FieldName) && hidden.Contains(item.FieldName);
        }
    }

    /// <summary>Feld -&gt; Spaltentitel. Die erste Nennung gewinnt, damit ein von Hand
    /// verfaelschtes Layout eine Karte nicht verdoppeln kann.</summary>
    private static Dictionary<string, string> BuildColumnAssignment(RecordDetailLayout layout)
    {
        var columnByField = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var column in layout.Columns)
        {
            foreach (var field in column.Fields)
            {
                if (!string.IsNullOrEmpty(field))
                    columnByField.TryAdd(field, column.Title);
            }
        }

        return columnByField;
    }

    /// <summary>
    /// Verteilt die Karten auf die Spalten, die das Layout nennt, und sortiert sie
    /// innerhalb jeder Spalte. Eine Karte, die das Layout nicht kennt, bleibt in ihrer
    /// Builder-Spalte.
    /// </summary>
    private static List<RecordDetailGroup> Regroup(
        IReadOnlyList<RecordDetailGroup> groups,
        RecordDetailLayout layout,
        IReadOnlyDictionary<string, string> columnByField)
    {
        var itemsByTitle = new Dictionary<string, List<RecordDetailItem>>(StringComparer.Ordinal);
        foreach (var group in groups)
            itemsByTitle[group.Title] = new List<RecordDetailItem>();

        foreach (var group in groups)
        {
            foreach (var item in group.Items)
            {
                var zielTitel = group.Title;
                if (!string.IsNullOrEmpty(item.FieldName)
                    && columnByField.TryGetValue(item.FieldName, out var gewuenscht)
                    && itemsByTitle.ContainsKey(gewuenscht))
                {
                    zielTitel = gewuenscht;
                }

                itemsByTitle[zielTitel].Add(item);
            }
        }

        var fieldRankByTitle = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var column in layout.Columns)
            fieldRankByTitle[column.Title] = RecordDetailOrderRanking.BuildRankLookup(column.Fields);

        var result = new List<RecordDetailGroup>(groups.Count);
        foreach (var group in groups)
        {
            var items = itemsByTitle[group.Title];
            if (fieldRankByTitle.TryGetValue(group.Title, out var rank) && rank.Count > 0)
                items = RecordDetailOrderRanking.StableOrder(items, x => x.FieldName, rank);

            result.Add(group with { Items = items });
        }

        return result;
    }

    private static IReadOnlyList<RecordDetailGroup> SortColumns(
        List<RecordDetailGroup> groups,
        RecordDetailLayout layout)
    {
        if (layout.Columns.Count == 0)
            return groups;

        var rank = RecordDetailOrderRanking.BuildRankLookup(
            layout.Columns.Select(c => c.Title).ToList());

        return RecordDetailOrderRanking.StableOrder(groups, x => x.Title, rank);
    }
}
