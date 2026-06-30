using System.Collections.ObjectModel;
using AuswertungPro.Next.UI.Collections;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ObservableCollectionContentControllerTests
{
    [Fact]
    public void ReplaceWith_entfernt_alte_elemente_und_haelt_reihenfolge_der_neuen()
    {
        var items = new ObservableCollection<string> { "alt-1", "alt-2" };

        ObservableCollectionContentController.ReplaceWith(items, new[] { "neu-1", "neu-2", "neu-3" });

        Assert.Equal(new[] { "neu-1", "neu-2", "neu-3" }, items);
    }

    [Fact]
    public void Append_fuegt_elemente_hinten_an_ohne_vorhandene_zu_loeschen()
    {
        var items = new ObservableCollection<string> { "alt" };

        ObservableCollectionContentController.Append(items, new[] { "neu-1", "neu-2" });

        Assert.Equal(new[] { "alt", "neu-1", "neu-2" }, items);
    }

    [Fact]
    public void ReplaceWith_akzeptiert_leere_quelle()
    {
        var items = new ObservableCollection<string> { "alt" };

        ObservableCollectionContentController.ReplaceWith(items, Array.Empty<string>());

        Assert.Empty(items);
    }
}
