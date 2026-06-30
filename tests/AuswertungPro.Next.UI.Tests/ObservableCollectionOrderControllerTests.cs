using System.Collections.ObjectModel;
using AuswertungPro.Next.UI.Collections;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ObservableCollectionOrderControllerTests
{
    [Fact]
    public void CanMoveByOffset_erkennt_gueltige_und_ungueltige_bewegungen()
    {
        var first = new Item("A");
        var second = new Item("B");
        var third = new Item("C");
        var items = new ObservableCollection<Item> { first, second, third };

        Assert.False(ObservableCollectionOrderController.CanMoveByOffset(items, null, -1));
        Assert.False(ObservableCollectionOrderController.CanMoveByOffset(items, first, -1));
        Assert.True(ObservableCollectionOrderController.CanMoveByOffset(items, second, -1));
        Assert.True(ObservableCollectionOrderController.CanMoveByOffset(items, second, 1));
        Assert.False(ObservableCollectionOrderController.CanMoveByOffset(items, third, 1));
        Assert.False(ObservableCollectionOrderController.CanMoveByOffset(items, new Item("X"), 1));
        Assert.False(ObservableCollectionOrderController.CanMoveByOffset(items, second, 0));
    }

    [Fact]
    public void TryMoveByOffset_verschiebt_element_und_meldet_erfolg()
    {
        var first = new Item("A");
        var second = new Item("B");
        var third = new Item("C");
        var items = new ObservableCollection<Item> { first, second, third };

        var moved = ObservableCollectionOrderController.TryMoveByOffset(items, second, -1);

        Assert.True(moved);
        Assert.Equal(new[] { "B", "A", "C" }, items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public void TryMoveByOffset_ignoriert_ungueltige_bewegung()
    {
        var first = new Item("A");
        var second = new Item("B");
        var items = new ObservableCollection<Item> { first, second };
        var original = items.ToArray();

        Assert.False(ObservableCollectionOrderController.TryMoveByOffset(items, first, -1));
        Assert.False(ObservableCollectionOrderController.TryMoveByOffset(items, new Item("X"), 1));
        Assert.False(ObservableCollectionOrderController.TryMoveByOffset(items, second, 0));

        Assert.Equal(original, items);
    }

    [Fact]
    public void Reorder_bringt_collection_in_angegebene_reihenfolge()
    {
        var first = new Item("A");
        var second = new Item("B");
        var third = new Item("C");
        var items = new ObservableCollection<Item> { first, second, third };

        ObservableCollectionOrderController.Reorder(items, new[] { third, first, second });

        Assert.Equal(new[] { "C", "A", "B" }, items.Select(i => i.Name).ToArray());
    }

    private sealed record Item(string Name);
}
