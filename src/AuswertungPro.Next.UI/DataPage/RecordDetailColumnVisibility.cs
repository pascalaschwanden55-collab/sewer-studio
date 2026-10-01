using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Entscheidet, welche Spalten die kompakte Detailansicht zeigt.
///
/// Eine Spalte ohne sichtbare Karte kostet nur Breite. Da sich die Spalten gleichmaessig
/// auf die volle Breite verteilen, bekommen die uebrigen den Platz automatisch.
///
/// Rein rechnend, ohne Oberflaeche.
/// </summary>
public static class RecordDetailColumnVisibility
{
    /// <summary>
    /// Liefert die anzuzeigenden Spalten.
    ///
    /// Im Anpassen-Modus bleibt eine leergeraeumte Spalte stehen — sonst gaebe es keinen
    /// Weg, wieder eine Karte hineinzuziehen.
    ///
    /// Die ausfuehrliche Ansicht (nicht kompakt) zeigt alles: dort stehen die Gruppen
    /// untereinander, eine leere kostet keine Breite, und die Dokumente gehoeren dazu.
    ///
    /// Aendert sich nichts, kommt dieselbe Liste zurueck — das erspart der Oberflaeche
    /// einen unnoetigen Neuaufbau.
    /// </summary>
    public static IReadOnlyList<RecordDetailGroup> Filter(
        IReadOnlyList<RecordDetailGroup> groups,
        bool isCustomizing,
        bool isCompact)
    {
        ArgumentNullException.ThrowIfNull(groups);

        if (!isCompact)
            return groups;

        var sichtbare = new List<RecordDetailGroup>(groups.Count);
        foreach (var group in groups)
        {
            if (group.Kind == RecordDetailGroupKind.Documents)
                continue;

            if (!isCustomizing && !HatSichtbareKarte(group))
                continue;

            sichtbare.Add(group);
        }

        return sichtbare.Count == groups.Count ? groups : sichtbare;
    }

    /// <summary>
    /// Sichtbar ist eine Karte nur, wenn beides zutrifft: die fachliche Regel
    /// (<c>IsVisible</c>, Sanierungs-Folgefelder) und die persoenliche Einstellung
    /// (<c>IsHiddenByUser</c>).
    /// </summary>
    private static bool HatSichtbareKarte(RecordDetailGroup group)
    {
        foreach (var item in group.Items)
        {
            if (item.IsVisible && !item.IsHiddenByUser)
                return true;
        }

        return false;
    }
}
