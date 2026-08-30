using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die Ziehoperationen des Anpassen-Modus auf der angezeigten Gruppenliste:
/// Karte innerhalb einer Spalte, Karte in eine andere Spalte, ganze Spalte.
/// </summary>
public sealed class RecordDetailDragOperationsTests
{
    private static RecordDetailItem Item(string fieldName)
        => new(fieldName, fieldName, _ => { }) { FieldName = fieldName };

    private static RecordDetailGroup Group(string title, params string[] fields)
        => new(title, string.Empty, fields.Select(Item).ToList(), RecordDetailGroupKind.Additional);

    private static string[] Fields(RecordDetailGroup group)
        => group.Items.Select(x => x.FieldName).ToArray();

    private static string[] Titles(IReadOnlyList<RecordDetailGroup> groups)
        => groups.Select(x => x.Title).ToArray();

    private static IReadOnlyList<RecordDetailGroup> Zwei()
        => new[] { Group("Links", "A", "B", "C"), Group("Rechts", "X", "Y") };

    // --- Finden -------------------------------------------------------------

    [Fact]
    public void TryLocateField_findet_Spalte_und_Position()
    {
        var groups = Zwei();

        Assert.True(RecordDetailDragOperations.TryLocateField(groups, groups[1].Items[1], out var title, out var index));
        Assert.Equal("Rechts", title);
        Assert.Equal(1, index);
    }

    [Fact]
    public void TryLocateField_meldet_eine_fremde_Karte_nicht()
    {
        Assert.False(RecordDetailDragOperations.TryLocateField(Zwei(), Item("FREMD"), out _, out var index));
        Assert.Equal(-1, index);
    }

    [Fact]
    public void TryLocateColumn_findet_die_Spalte()
    {
        var groups = Zwei();

        Assert.True(RecordDetailDragOperations.TryLocateColumn(groups, groups[1], out var index));
        Assert.Equal(1, index);
        Assert.False(RecordDetailDragOperations.TryLocateColumn(groups, Group("Fremd"), out var fehlt));
        Assert.Equal(-1, fehlt);
    }

    // --- Karte innerhalb ihrer Spalte ---------------------------------------

    [Fact]
    public void MoveField_verschiebt_innerhalb_der_Spalte()
    {
        var groups = Zwei();

        var result = RecordDetailDragOperations.MoveField(groups, "Links", 2, "Links", 0);

        Assert.NotNull(result);
        Assert.Equal(new[] { "C", "A", "B" }, Fields(result![0]));
        Assert.Equal(new[] { "X", "Y" }, Fields(result[1]));
    }

    [Fact]
    public void MoveField_ohne_Positionsaenderung_meldet_nichts()
    {
        Assert.Null(RecordDetailDragOperations.MoveField(Zwei(), "Links", 1, "Links", 1));
    }

    // --- Karte in eine andere Spalte ----------------------------------------

    [Fact]
    public void MoveField_holt_eine_Karte_in_die_andere_Spalte()
    {
        var groups = Zwei();

        var result = RecordDetailDragOperations.MoveField(groups, "Links", 1, "Rechts", 0);

        Assert.NotNull(result);
        Assert.Equal(new[] { "A", "C" }, Fields(result![0]));
        Assert.Equal(new[] { "B", "X", "Y" }, Fields(result[1]));
    }

    [Fact]
    public void MoveField_kann_eine_Karte_ans_Ende_der_anderen_Spalte_setzen()
    {
        var groups = Zwei();

        // Zielposition 2 = hinter das letzte vorhandene Element der Zielspalte.
        var result = RecordDetailDragOperations.MoveField(groups, "Links", 0, "Rechts", 2);

        Assert.NotNull(result);
        Assert.Equal(new[] { "B", "C" }, Fields(result![0]));
        Assert.Equal(new[] { "X", "Y", "A" }, Fields(result[1]));
    }

    [Fact]
    public void MoveField_leert_eine_Spalte_notfalls_ganz()
    {
        var groups = new[] { Group("Links", "A"), Group("Rechts", "X") };

        var result = RecordDetailDragOperations.MoveField(groups, "Links", 0, "Rechts", 0);

        Assert.NotNull(result);
        Assert.Empty(result![0].Items);
        Assert.Equal(new[] { "A", "X" }, Fields(result[1]));
    }

    [Theory]
    [InlineData("Gibt_es_nicht", 0, "Rechts", 0)]
    [InlineData("Links", 0, "Gibt_es_nicht", 0)]
    [InlineData("Links", -1, "Rechts", 0)]
    [InlineData("Links", 3, "Rechts", 0)]
    [InlineData("Links", 0, "Rechts", -1)]
    [InlineData("Links", 0, "Rechts", 3)]
    public void MoveField_weist_unmoegliche_Angaben_ab(string fromTitle, int fromIndex, string toTitle, int toIndex)
    {
        Assert.Null(RecordDetailDragOperations.MoveField(Zwei(), fromTitle, fromIndex, toTitle, toIndex));
    }

    // --- Spalte -------------------------------------------------------------

    [Fact]
    public void MoveColumn_verschiebt_die_Spalte_mitsamt_ihren_Karten()
    {
        var groups = Zwei();

        var result = RecordDetailDragOperations.MoveColumn(groups, 1, 0);

        Assert.NotNull(result);
        Assert.Equal(new[] { "Rechts", "Links" }, Titles(result!));
        Assert.Equal(new[] { "X", "Y" }, Fields(result![0]));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    public void MoveColumn_weist_unmoegliche_Angaben_ab(int fromIndex, int toIndex)
    {
        Assert.Null(RecordDetailDragOperations.MoveColumn(Zwei(), fromIndex, toIndex));
    }

    // --- Ablageposition -----------------------------------------------------

    [Theory]
    // Innerhalb derselben Spalte rutscht alles hinter der entnommenen Karte auf.
    [InlineData(2, 0, false, 0)]
    [InlineData(0, 2, true, 2)]
    public void ResolveDropTarget_innerhalb_derselben_Spalte(int fromIndex, int targetIndex, bool insertAfter, int expected)
    {
        Assert.Equal(expected, RecordDetailDragOperations.ResolveDropTarget(fromIndex, targetIndex, insertAfter, count: 3, sameColumn: true));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, false)]
    public void ResolveDropTarget_meldet_in_derselben_Spalte_keine_Aenderung(int fromIndex, int targetIndex, bool insertAfter)
    {
        Assert.Equal(-1, RecordDetailDragOperations.ResolveDropTarget(fromIndex, targetIndex, insertAfter, count: 3, sameColumn: true));
    }

    [Theory]
    // In einer FREMDEN Spalte wird nichts entnommen - die Zielstelle ist unmittelbar
    // die Einfuegestelle, und hinter dem letzten Element ist sie erlaubt.
    [InlineData(0, false, 0)]
    [InlineData(0, true, 1)]
    [InlineData(1, true, 2)]
    public void ResolveDropTarget_in_einer_fremden_Spalte(int targetIndex, bool insertAfter, int expected)
    {
        Assert.Equal(expected, RecordDetailDragOperations.ResolveDropTarget(
            fromIndex: 0, targetIndex, insertAfter, count: 2, sameColumn: false));
    }

    [Fact]
    public void ResolveDropTarget_erlaubt_die_erste_Karte_einer_leeren_Spalte()
    {
        Assert.Equal(0, RecordDetailDragOperations.ResolveDropTarget(
            fromIndex: 0, targetIndex: 0, insertAfter: false, count: 0, sameColumn: false));
    }

    [Fact]
    public void ResolveDropTarget_weist_eine_leere_eigene_Spalte_ab()
    {
        Assert.Equal(-1, RecordDetailDragOperations.ResolveDropTarget(
            fromIndex: 0, targetIndex: 0, insertAfter: false, count: 0, sameColumn: true));
    }
}
