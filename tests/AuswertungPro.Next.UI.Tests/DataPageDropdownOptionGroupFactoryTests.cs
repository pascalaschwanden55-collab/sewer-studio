using System.Collections.ObjectModel;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageDropdownOptionGroupFactoryTests
{
    private static readonly string[] FixedEigentuemerOptions =
        ["Kanton", "Bund", "AWU", "Gemeinde", "Privat"];

    [Fact]
    public void Create_ordnet_exakte_Titel_und_Resetwerte_den_fuenf_Gruppen_zu()
    {
        var options = CreateOptions();
        var previews = new List<(string Message, string Title)>();
        var saveCalls = 0;
        var groups = DataPageDropdownOptionGroupFactory.Create(
            options,
            FixedEigentuemerOptions,
            new DropdownOptionGroupActions(
                _ => new DropdownOptionEditorResult(false, Array.Empty<string>()),
                (message, title) => previews.Add((message, title)),
                () => saveCalls++));

        groups.Sanieren.Reset();
        groups.Eigentuemer.Reset();
        groups.Pruefungsresultat.Reset();
        groups.Referenzpruefung.Reset();
        groups.EmpfohleneSanierungsmassnahmen.Reset();

        groups.Sanieren.Preview();
        groups.Eigentuemer.Preview();
        groups.Pruefungsresultat.Preview();
        groups.Referenzpruefung.Preview();
        groups.EmpfohleneSanierungsmassnahmen.Preview();

        Assert.Equal(new[] { "Nein", "Ja" }, options.Sanieren);
        Assert.Equal(FixedEigentuemerOptions, options.Eigentuemer);
        Assert.Equal(
            new[]
            {
                "Pruefung bestanden",
                "Pruefung knapp nicht bestanden",
                "Pruefung nicht bestanden (grob undicht)",
                "Keine"
            },
            options.Pruefungsresultat);
        Assert.Equal(new[] { "Ja", "Nein" }, options.Referenzpruefung);
        Assert.Equal(new[] { "" }, options.EmpfohleneSanierungsmassnahmen);
        Assert.Equal(
            new[]
            {
                "Sanieren-Liste",
                "Eigentuemer-Liste",
                "Pruefungsresultat-Liste",
                "Referenzpruefung-Liste",
                "Sanierungsmassnahmen-Liste"
            },
            previews.Select(x => x.Title));
        Assert.Equal(5, saveCalls);
    }

    [Fact]
    public void Eigentuemer_ist_nicht_gesperrt_und_speichert_Nullaktionen_nicht()
    {
        var options = CreateOptions();
        DropdownOptionList.ReplaceWith(options.Eigentuemer, FixedEigentuemerOptions);
        var saveCalls = 0;
        var groups = DataPageDropdownOptionGroupFactory.Create(
            options,
            FixedEigentuemerOptions,
            new DropdownOptionGroupActions(
                _ => new DropdownOptionEditorResult(false, Array.Empty<string>()),
                (_, _) => { },
                () => saveCalls++));

        groups.Eigentuemer.Add(null);
        groups.Eigentuemer.Add("privat");
        groups.Eigentuemer.Remove("fehlt");

        Assert.Equal(FixedEigentuemerOptions, options.Eigentuemer);
        Assert.Equal(0, saveCalls);

        groups.Eigentuemer.Add("Firma");

        Assert.Equal("Firma", options.Eigentuemer[0]);
        Assert.Equal(1, saveCalls);
    }

    private static DataPageDropdownOptionCollections CreateOptions()
        => new(
            new ObservableCollection<string> { "Sanieren alt" },
            new ObservableCollection<string> { "Eigentuemer alt" },
            new ObservableCollection<string> { "Pruefung alt" },
            new ObservableCollection<string> { "Referenz alt" },
            new ObservableCollection<string> { "Massnahme alt" },
            new ObservableCollection<string> { "Material alt" });
}
