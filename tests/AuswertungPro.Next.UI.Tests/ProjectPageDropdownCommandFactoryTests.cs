using System.Collections.ObjectModel;
using System.Collections.Specialized;
using AuswertungPro.Next.UI.ProjectPage;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectPageDropdownCommandFactoryTests
{
    [Fact]
    public void Sanieren_Edit_verwendet_den_beim_Ausfuehren_aktuellen_Wert_und_speichert_einmal()
    {
        var sanieren = new ObservableCollection<string> { "Alt" };
        var eigentuemer = new ObservableCollection<string> { "Privat" };
        var currentValue = "Anfang";
        var saveCalls = 0;
        var commands = Create(
            sanieren,
            eigentuemer,
            () => currentValue,
            editOptions: current =>
            {
                Assert.Equal(["Alt"], current);
                return new DropdownOptionEditorResult(true, ["Nein"]);
            },
            save: () => saveCalls++);
        currentValue = "Spaeter";

        commands.Sanieren.Edit.Execute(null);

        Assert.Equal(["Spaeter", "Nein"], sanieren);
        Assert.Equal(1, saveCalls);
    }

    [Fact]
    public void Sanieren_Edit_erzeugt_fuer_aktuellen_Wert_keine_Dublette_ohne_Beachtung_der_Grossschreibung()
    {
        var sanieren = new ObservableCollection<string> { "Alt" };
        var commands = Create(
            sanieren,
            new ObservableCollection<string>(),
            () => "ja",
            editOptions: _ => new DropdownOptionEditorResult(true, ["JA", "Nein"]));

        commands.Sanieren.Edit.Execute(null);

        Assert.Equal(["JA", "Nein"], sanieren);
    }

    [Fact]
    public void Sanieren_Editabbruch_aendert_und_speichert_nichts()
    {
        var sanieren = new ObservableCollection<string> { "Alt" };
        var saveCalls = 0;
        var currentValueReads = 0;
        var commands = Create(
            sanieren,
            new ObservableCollection<string>(),
            () =>
            {
                currentValueReads++;
                return "Aktuell";
            },
            editOptions: _ => new DropdownOptionEditorResult(false, ["Neu"]),
            save: () => saveCalls++);

        commands.Sanieren.Edit.Execute(null);

        Assert.Equal(["Alt"], sanieren);
        Assert.Equal(0, saveCalls);
        Assert.Equal(0, currentValueReads);
    }

    [Fact]
    public void Sanieren_Edit_bewahrt_die_bisherige_Aenderungsreihenfolge_der_echten_Sammlung()
    {
        var sanieren = new ObservableCollection<string> { "Alt" };
        var changes = new List<(NotifyCollectionChangedAction Action, int Index, string? Item)>();
        sanieren.CollectionChanged += (_, e) => changes.Add((
            e.Action,
            e.NewStartingIndex,
            e.NewItems?.Cast<string>().SingleOrDefault()));
        var commands = Create(
            sanieren,
            new ObservableCollection<string>(),
            () => "Aktuell",
            editOptions: _ => new DropdownOptionEditorResult(true, ["Nein", "Ja"]));

        commands.Sanieren.Edit.Execute(null);

        Assert.Equal(
            [
                (NotifyCollectionChangedAction.Reset, -1, null),
                (NotifyCollectionChangedAction.Add, 0, "Nein"),
                (NotifyCollectionChangedAction.Add, 1, "Ja"),
                (NotifyCollectionChangedAction.Add, 0, "Aktuell")
            ],
            changes);
    }

    [Fact]
    public void Sanieren_Reset_Add_und_Remove_bewahren_Aenderungsgrenzen()
    {
        var sanieren = new ObservableCollection<string> { "Alt" };
        var saveCalls = 0;
        var commands = Create(
            sanieren,
            new ObservableCollection<string>(),
            () => "Sonderwert",
            save: () => saveCalls++);

        commands.Sanieren.Reset.Execute(null);
        commands.Sanieren.Add.Execute("  Vielleicht  ");
        commands.Sanieren.Add.Execute("vielleicht");
        commands.Sanieren.Remove.Execute("JA");
        commands.Sanieren.Remove.Execute("fehlt");

        Assert.Equal(["Vielleicht", "Nein"], sanieren);
        Assert.Equal(3, saveCalls);
    }

    [Fact]
    public void Eigentuemer_Edit_Reset_Add_und_Remove_stellen_feste_Liste_her_und_speichern_immer()
    {
        var fixedItems = FixedEigentuemerOptions();
        var eigentuemer = new ObservableCollection<string> { "Falsch" };
        var saveCalls = 0;
        var commands = Create(
            new ObservableCollection<string>(),
            eigentuemer,
            () => "Nein",
            fixedEigentuemerOptions: fixedItems,
            editOptions: _ => new DropdownOptionEditorResult(true, ["Beliebig"]),
            save: () => saveCalls++);

        commands.Eigentuemer.Edit.Execute(null);
        eigentuemer.Add("Falsch");
        commands.Eigentuemer.Reset.Execute(null);
        eigentuemer.Add("Falsch");
        commands.Eigentuemer.Add.Execute("Noch eins");
        eigentuemer.Add("Falsch");
        commands.Eigentuemer.Remove.Execute("Privat");

        Assert.Equal(fixedItems, eigentuemer);
        Assert.Equal(4, saveCalls);
    }

    [Fact]
    public void Eigentuemer_Reset_speichert_bei_bereits_exakter_Liste_ohne_Sammlungsereignis()
    {
        var fixedItems = FixedEigentuemerOptions();
        var eigentuemer = new ObservableCollection<string>(fixedItems);
        var collectionChanges = 0;
        var saveCalls = 0;
        eigentuemer.CollectionChanged += (_, _) => collectionChanges++;
        var commands = Create(
            new ObservableCollection<string>(),
            eigentuemer,
            () => "Nein",
            fixedEigentuemerOptions: fixedItems,
            save: () => saveCalls++);

        commands.Eigentuemer.Reset.Execute(null);

        Assert.Equal(fixedItems, eigentuemer);
        Assert.Equal(0, collectionChanges);
        Assert.Equal(1, saveCalls);
    }

    [Fact]
    public void Preview_verwendet_bisherige_Titel_und_Zeilenumbrueche()
    {
        var messages = new List<(string Message, string Title)>();
        var commands = Create(
            new ObservableCollection<string> { "Nein", "Ja" },
            new ObservableCollection<string> { "Gemeinde", "Privat" },
            () => "Nein",
            showInfo: (message, title) => messages.Add((message, title)));

        commands.Sanieren.Preview.Execute(null);
        commands.Eigentuemer.Preview.Execute(null);

        Assert.Equal(
            [
                ("Nein\nJa", "Sanieren-Liste"),
                ("Gemeinde\nPrivat", "Eigentuemer-Liste")
            ],
            messages);
    }

    private static ProjectPageDropdownCommands Create(
        ObservableCollection<string> sanieren,
        ObservableCollection<string> eigentuemer,
        Func<string> getCurrentSanierenValue,
        IReadOnlyList<string>? fixedEigentuemerOptions = null,
        Func<IReadOnlyList<string>, DropdownOptionEditorResult>? editOptions = null,
        Action<string, string>? showInfo = null,
        Action? save = null)
        => ProjectPageDropdownCommandFactory.Create(
            sanieren,
            eigentuemer,
            fixedEigentuemerOptions ?? FixedEigentuemerOptions(),
            getCurrentSanierenValue,
            new DropdownOptionGroupActions(
                editOptions ?? (_ => new DropdownOptionEditorResult(true, [])),
                showInfo ?? ((_, _) => { }),
                save ?? (() => { })));

    private static IReadOnlyList<string> FixedEigentuemerOptions()
        => ["Kanton", "Bund", "AWU", "Gemeinde", "Privat"];
}
