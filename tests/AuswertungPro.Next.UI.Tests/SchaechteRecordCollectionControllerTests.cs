using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteRecordCollectionControllerTests
{
    [Fact]
    public void Add_initialisiert_alle_Spalten_und_verwendet_Anzahl_plus_eins()
    {
        var records = Records(Record("A", "9"));
        var controller = Create(records, ["Schachtnummer", "Nr.", "Bemerkung"]);

        var added = controller.Add();

        Assert.Same(added, records[1]);
        Assert.Equal("", added.GetFieldValue("Schachtnummer"));
        Assert.Equal("2", added.GetFieldValue("Nr."));
        Assert.Equal("", added.GetFieldValue("Bemerkung"));
    }

    [Fact]
    public void Add_verwendet_nach_Projektwechsel_die_aktuelle_Sammlung()
    {
        var first = Records();
        var second = Records();
        var current = first;
        var controller = new SchaechteRecordCollectionController(
            () => current,
            () => new[] { "Nr." },
            new object());

        var firstAdded = controller.Add();
        current = second;
        var secondAdded = controller.Add();

        Assert.Same(firstAdded, Assert.Single(first));
        Assert.Same(secondAdded, Assert.Single(second));
        Assert.Equal("1", firstAdded.GetFieldValue("Nr."));
        Assert.Equal("1", secondAdded.GetFieldValue("Nr."));
    }

    [Fact]
    public void TryRemove_ignoriert_fehlende_oder_fremde_Auswahl()
    {
        var records = Records(Record("A"), Record("B"));
        var original = records.ToArray();
        var controller = Create(records);

        Assert.False(controller.TryRemove(null, out var afterNull));
        Assert.False(controller.TryRemove(Record("fremd"), out var afterForeign));

        Assert.Null(afterNull);
        Assert.Null(afterForeign);
        Assert.Equal(original, records);
    }

    [Fact]
    public void TryRemove_waehlt_nach_mittlerem_Schacht_den_naechsten()
    {
        var first = Record("A");
        var selected = Record("B");
        var next = Record("C");
        var records = Records(first, selected, next);
        var controller = Create(records);

        var removed = controller.TryRemove(selected, out var nextSelection);

        Assert.True(removed);
        Assert.Equal(new[] { first, next }, records);
        Assert.Same(next, nextSelection);
    }

    [Fact]
    public void TryRemove_waehlt_nach_letztem_Schacht_den_vorherigen()
    {
        var first = Record("A");
        var selected = Record("B");
        var records = Records(first, selected);
        var controller = Create(records);

        var removed = controller.TryRemove(selected, out var nextSelection);

        Assert.True(removed);
        Assert.Equal(new[] { first }, records);
        Assert.Same(first, nextSelection);
    }

    [Fact]
    public void TryRemove_einzigen_Schacht_liefert_leere_Auswahl()
    {
        var selected = Record("A");
        var records = Records(selected);
        var controller = Create(records);

        var removed = controller.TryRemove(selected, out var nextSelection);

        Assert.True(removed);
        Assert.Empty(records);
        Assert.Null(nextSelection);
    }

    [Fact]
    public void CanMove_prueft_ersten_mittleren_letzten_und_fremden_Schacht()
    {
        var first = Record("A");
        var middle = Record("B");
        var last = Record("C");
        var records = Records(first, middle, last);
        var controller = Create(records);

        Assert.False(controller.CanMoveUp(null));
        Assert.False(controller.CanMoveDown(null));
        Assert.False(controller.CanMoveUp(Record("fremd")));
        Assert.False(controller.CanMoveDown(Record("fremd")));
        Assert.False(controller.CanMoveUp(first));
        Assert.True(controller.CanMoveDown(first));
        Assert.True(controller.CanMoveUp(middle));
        Assert.True(controller.CanMoveDown(middle));
        Assert.True(controller.CanMoveUp(last));
        Assert.False(controller.CanMoveDown(last));
    }

    [Fact]
    public void MoveUp_und_MoveDown_aendern_Reihenfolge_nur_wenn_moeglich()
    {
        var first = Record("A");
        var middle = Record("B");
        var last = Record("C");
        var records = Records(first, middle, last);
        var controller = Create(records);

        Assert.True(controller.TryMoveUp(middle));
        Assert.Equal(new[] { middle, first, last }, records);
        Assert.False(controller.TryMoveUp(middle));

        Assert.True(controller.TryMoveDown(middle));
        Assert.Equal(new[] { first, middle, last }, records);
        Assert.False(controller.TryMoveDown(last));
    }

    [Fact]
    public void MoveToPosition_ist_einbasiert_geklammert_und_erkennt_gleiche_Position()
    {
        var first = Record("A");
        var selected = Record("B");
        var last = Record("C");
        var records = Records(first, selected, last);
        var controller = Create(records);

        Assert.True(controller.TryMoveToPosition(selected, 0));
        Assert.Equal(new[] { selected, first, last }, records);

        Assert.True(controller.TryMoveToPosition(selected, 99));
        Assert.Equal(new[] { first, last, selected }, records);

        Assert.False(controller.TryMoveToPosition(selected, 3));
        Assert.Equal(new[] { first, last, selected }, records);
    }

    [Fact]
    public void Renumber_schreibt_eins_bis_n_auch_wenn_Werte_bereits_stimmen()
    {
        var first = Record("A", "1");
        var second = Record("B", "2");
        var records = Records(first, second);
        var firstChanges = 0;
        var secondChanges = 0;
        first.PropertyChanged += (_, _) => firstChanges++;
        second.PropertyChanged += (_, _) => secondChanges++;
        var controller = Create(records, ["Nr."]);

        controller.Renumber();

        Assert.Equal(new[] { "1", "2" }, records.Select(x => x.GetFieldValue("Nr.")).ToArray());
        Assert.True(firstChanges > 0);
        Assert.True(secondChanges > 0);
    }

    [Fact]
    public void Renumber_ohne_Nummernfeld_bleibt_Nullaktion()
    {
        var record = Record("A");
        var records = Records(record);
        var changes = 0;
        record.PropertyChanged += (_, _) => changes++;
        var controller = Create(records, ["Schachtnummer"]);

        controller.Renumber();

        Assert.Equal(0, changes);
        Assert.False(record.Fields.ContainsKey("Nr."));
    }

    // Verschieben auf Position heisst EINFUEGEN, nicht tauschen: Der Zielschacht und
    // alle dazwischen ruecken eine Stelle weiter, ihre Reihenfolge untereinander bleibt.
    [Fact]
    public void TryMoveToPosition_schiebt_nach_oben_ein_und_tauscht_nicht()
    {
        var records = Records(
            Record("A"), Record("B"), Record("C"), Record("D"), Record("E"));

        Assert.True(Create(records).TryMoveToPosition(records[4], targetPosition: 2));

        Assert.Equal(
            new[] { "A", "E", "B", "C", "D" },
            records.Select(x => x.GetFieldValue("Schachtnummer")));
    }

    [Fact]
    public void TryMoveToPosition_schiebt_nach_unten_ein_und_tauscht_nicht()
    {
        var records = Records(
            Record("A"), Record("B"), Record("C"), Record("D"), Record("E"));

        Assert.True(Create(records).TryMoveToPosition(records[1], targetPosition: 4));

        Assert.Equal(
            new[] { "A", "C", "D", "B", "E" },
            records.Select(x => x.GetFieldValue("Schachtnummer")));
    }

    [Fact]
    public void TryMoveToPosition_laesst_die_gleiche_Position_unveraendert()
    {
        var records = Records(Record("A"), Record("B"), Record("C"));

        Assert.False(Create(records).TryMoveToPosition(records[1], targetPosition: 2));

        Assert.Equal(
            new[] { "A", "B", "C" },
            records.Select(x => x.GetFieldValue("Schachtnummer")));
    }

    [Fact]
    public void Renumber_zaehlt_nach_dem_Verschieben_lueckenlos_durch()
    {
        var records = Records(
            Record("A", "1"), Record("B", "2"), Record("C", "3"), Record("D", "4"));
        var controller = Create(records, ["Schachtnummer", "Nr."]);

        Assert.True(controller.TryMoveToPosition(records[3], targetPosition: 2));
        controller.Renumber();

        Assert.Equal(
            new[] { "A", "D", "B", "C" },
            records.Select(x => x.GetFieldValue("Schachtnummer")));
        Assert.Equal(
            new[] { "1", "2", "3", "4" },
            records.Select(x => x.GetFieldValue("Nr.")));
    }

    private static SchaechteRecordCollectionController Create(
        ObservableCollection<SchachtRecord> records,
        IReadOnlyList<string>? columns = null,
        object? collectionLock = null)
        => new(
            () => records,
            () => columns ?? Array.Empty<string>(),
            collectionLock ?? new object());

    private static ObservableCollection<SchachtRecord> Records(params SchachtRecord[] records)
        => new(records);

    private static SchachtRecord Record(string name, string? nr = null)
    {
        var record = new SchachtRecord();
        record.Fields["Schachtnummer"] = name;
        if (nr is not null)
            record.Fields["Nr."] = nr;
        return record;
    }
}
