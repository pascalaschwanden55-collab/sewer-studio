using System.Collections.ObjectModel;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteDropdownCommandFactoryTests
{
    private static readonly string[] FixedEigentuemerOptions =
        ["Kanton", "Bund", "AWU", "Gemeinde", "Privat"];

    [Fact]
    public void Create_preserves_titles_reset_values_and_save_boundaries()
    {
        var options = CreateOptions();
        var previews = new List<(string Message, string Title)>();
        var saveCalls = 0;
        var commands = Create(
            options,
            showInfo: (message, title) => previews.Add((message, title)),
            save: () => saveCalls++);

        commands.Sanieren.Reset.Execute(null);
        commands.Eigentuemer.Reset.Execute(null);
        commands.Pruefungsresultat.Reset.Execute(null);
        commands.Referenzpruefung.Reset.Execute(null);
        commands.Sanieren.Preview.Execute(null);
        commands.Eigentuemer.Preview.Execute(null);
        commands.Pruefungsresultat.Preview.Execute(null);
        commands.Referenzpruefung.Preview.Execute(null);

        Assert.Equal(["Nein", "Ja"], options.Sanieren);
        Assert.Equal(FixedEigentuemerOptions, options.Eigentuemer);
        Assert.Equal(
            [
                "Pruefung bestanden",
                "Pruefung knapp nicht bestanden",
                "Pruefung nicht bestanden (grob undicht)",
                "Keine"
            ],
            options.Pruefungsresultat);
        Assert.Equal(["Ja", "Nein"], options.Referenzpruefung);
        Assert.Equal(
            [
                "Sanieren-Liste",
                "Eigentuemer-Liste",
                "Pruefungsresultat-Liste",
                "Referenzpruefung-Liste"
            ],
            previews.Select(item => item.Title));
        Assert.Equal(4, saveCalls);
    }

    [Fact]
    public void Eigentuemer_commands_restore_locked_list_and_save_each_action()
    {
        var options = CreateOptions();
        var saveCalls = 0;
        var commands = Create(
            options,
            editOptions: _ => new DropdownOptionEditorResult(true, ["Beliebig"]),
            save: () => saveCalls++);

        commands.Eigentuemer.Edit.Execute(null);
        options.Eigentuemer.Add("Falsch");
        commands.Eigentuemer.Add.Execute("Noch eins");
        options.Eigentuemer.Add("Falsch");
        commands.Eigentuemer.Remove.Execute("Privat");

        Assert.Equal(FixedEigentuemerOptions, options.Eigentuemer);
        Assert.Equal(3, saveCalls);
    }

    [Fact]
    public void Commands_route_each_option_group_independently()
    {
        var options = CreateOptions();
        var saveCalls = 0;
        var commands = Create(options, save: () => saveCalls++);

        commands.Sanieren.Add.Execute("Vielleicht");
        commands.Pruefungsresultat.Add.Execute("Offen");
        commands.Referenzpruefung.Add.Execute("Unbekannt");

        Assert.Equal("Vielleicht", options.Sanieren[0]);
        Assert.Equal("Offen", options.Pruefungsresultat[0]);
        Assert.Equal("Unbekannt", options.Referenzpruefung[0]);
        Assert.DoesNotContain("Vielleicht", options.Pruefungsresultat);
        Assert.DoesNotContain("Offen", options.Referenzpruefung);
        Assert.Equal(3, saveCalls);
    }

    private static SchaechteDropdownCommands Create(
        SchaechteDropdownOptionCollections options,
        Func<IReadOnlyList<string>, DropdownOptionEditorResult>? editOptions = null,
        Action<string, string>? showInfo = null,
        Action? save = null)
        => SchaechteDropdownCommandFactory.Create(
            options,
            FixedEigentuemerOptions,
            new DropdownOptionGroupActions(
                editOptions ?? (_ => new DropdownOptionEditorResult(false, [])),
                showInfo ?? ((_, _) => { }),
                save ?? (() => { })));

    private static SchaechteDropdownOptionCollections CreateOptions()
        => new(
            new ObservableCollection<string> { "Sanieren alt" },
            new ObservableCollection<string> { "Eigentuemer alt" },
            new ObservableCollection<string> { "Pruefung alt" },
            new ObservableCollection<string> { "Referenz alt" });
}
