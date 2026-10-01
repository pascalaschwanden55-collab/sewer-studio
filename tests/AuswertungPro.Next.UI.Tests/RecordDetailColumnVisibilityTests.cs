using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Welche Spalten die kompakte Detailansicht ueberhaupt zeigt. Eine Spalte ohne sichtbare
/// Karte kostet nur Platz — die uebrigen sollen ihn bekommen.
/// </summary>
public sealed class RecordDetailColumnVisibilityTests
{
    private static RecordDetailItem Item(string fieldName, bool visible = true, bool hidden = false)
        => new(fieldName, fieldName, _ => { }) { FieldName = fieldName, IsVisible = visible, IsHiddenByUser = hidden };

    private static RecordDetailGroup Group(string title, RecordDetailGroupKind kind, params RecordDetailItem[] items)
        => new(title, string.Empty, items, kind);

    private static string[] Titles(IReadOnlyList<RecordDetailGroup> groups)
        => groups.Select(x => x.Title).ToArray();

    private static IReadOnlyList<RecordDetailGroup> Filter(
        IReadOnlyList<RecordDetailGroup> groups,
        bool isCustomizing = false,
        bool isCompact = true)
        => RecordDetailColumnVisibility.Filter(groups, isCustomizing, isCompact);

    [Fact]
    public void Eine_Spalte_ohne_Karten_faellt_weg()
    {
        var groups = new[]
        {
            Group("Stammdaten", RecordDetailGroupKind.MasterData, Item("A")),
            Group("Weitere", RecordDetailGroupKind.Additional)
        };

        Assert.Equal(new[] { "Stammdaten" }, Titles(Filter(groups)));
    }

    [Fact]
    public void Eine_Spalte_mit_lauter_ausgeblendeten_Karten_faellt_weg()
    {
        var groups = new[]
        {
            Group("Stammdaten", RecordDetailGroupKind.MasterData, Item("A")),
            Group("Weitere", RecordDetailGroupKind.Additional, Item("B", hidden: true), Item("C", hidden: true))
        };

        Assert.Equal(new[] { "Stammdaten" }, Titles(Filter(groups)));
    }

    [Fact]
    public void Eine_Spalte_mit_lauter_fachlich_unsichtbaren_Karten_faellt_weg()
    {
        // "Sanieren = Nein" blendet die Folgefelder aus. Bleibt keine Karte uebrig,
        // braucht die Spalte auch keinen Platz.
        var groups = new[]
        {
            Group("Stammdaten", RecordDetailGroupKind.MasterData, Item("A")),
            Group("Sanierung", RecordDetailGroupKind.RenovationCosts, Item("Kosten", visible: false))
        };

        Assert.Equal(new[] { "Stammdaten" }, Titles(Filter(groups)));
    }

    [Fact]
    public void Eine_einzige_sichtbare_Karte_haelt_die_Spalte()
    {
        var groups = new[]
        {
            Group("Sanierung", RecordDetailGroupKind.RenovationCosts,
                Item("Sanieren_JaNein"),
                Item("Kosten", visible: false))
        };

        Assert.Equal(new[] { "Sanierung" }, Titles(Filter(groups)));
    }

    [Fact]
    public void Im_Anpassen_Modus_bleibt_die_leere_Spalte_stehen()
    {
        // Sonst gaebe es keinen Weg, wieder eine Karte hineinzuziehen.
        var groups = new[]
        {
            Group("Stammdaten", RecordDetailGroupKind.MasterData, Item("A")),
            Group("Weitere", RecordDetailGroupKind.Additional)
        };

        Assert.Equal(new[] { "Stammdaten", "Weitere" }, Titles(Filter(groups, isCustomizing: true)));
    }

    [Fact]
    public void Dokumente_bleiben_in_der_kompakten_Ansicht_aussen_vor()
    {
        var groups = new[]
        {
            Group("Stammdaten", RecordDetailGroupKind.MasterData, Item("A")),
            Group("Dokumente", RecordDetailGroupKind.Documents, Item("Link"))
        };

        Assert.Equal(new[] { "Stammdaten" }, Titles(Filter(groups)));
        // Auch beim Anpassen nicht - sonst liesse sich eine Spalte gestalten, die
        // hinterher niemand zu sehen bekommt.
        Assert.Equal(new[] { "Stammdaten" }, Titles(Filter(groups, isCustomizing: true)));
    }

    [Fact]
    public void Die_ausfuehrliche_Ansicht_zeigt_weiterhin_alles()
    {
        // Untereinander statt nebeneinander: dort kostet eine leere Gruppe keine Breite,
        // und die Dokumente gehoeren dazu.
        var groups = new[]
        {
            Group("Stammdaten", RecordDetailGroupKind.MasterData, Item("A")),
            Group("Weitere", RecordDetailGroupKind.Additional),
            Group("Dokumente", RecordDetailGroupKind.Documents, Item("Link"))
        };

        Assert.Same(groups, Filter(groups, isCompact: false));
    }

    [Fact]
    public void Ohne_Aenderung_bleibt_es_dieselbe_Liste()
    {
        // Spart in der Oberflaeche einen unnoetigen Neuaufbau der Spalten.
        var groups = new[]
        {
            Group("Stammdaten", RecordDetailGroupKind.MasterData, Item("A")),
            Group("Zustand", RecordDetailGroupKind.Condition, Item("B"))
        };

        Assert.Same(groups, Filter(groups));
    }

    [Fact]
    public void Eine_leere_Eingabe_bleibt_leer()
    {
        Assert.Empty(Filter(Array.Empty<RecordDetailGroup>()));
    }
}
