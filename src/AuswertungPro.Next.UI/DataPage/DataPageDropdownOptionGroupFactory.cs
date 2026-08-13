using System.Collections.ObjectModel;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageDropdownOptionCollections(
    ObservableCollection<string> Sanieren,
    ObservableCollection<string> Eigentuemer,
    ObservableCollection<string> Pruefungsresultat,
    ObservableCollection<string> Referenzpruefung,
    ObservableCollection<string> EmpfohleneSanierungsmassnahmen,
    ObservableCollection<string> Rohrmaterial);

public sealed record DataPageDropdownOptionGroups(
    DropdownOptionGroupController Sanieren,
    DropdownOptionGroupController Eigentuemer,
    DropdownOptionGroupController Pruefungsresultat,
    DropdownOptionGroupController Referenzpruefung,
    DropdownOptionGroupController EmpfohleneSanierungsmassnahmen,
    DropdownOptionGroupController Rohrmaterial);

public static class DataPageDropdownOptionGroupFactory
{
    public static DataPageDropdownOptionGroups Create(
        DataPageDropdownOptionCollections options,
        IReadOnlyList<string> fixedEigentuemerOptions,
        DropdownOptionGroupActions actions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fixedEigentuemerOptions);
        ArgumentNullException.ThrowIfNull(actions);

        return new DataPageDropdownOptionGroups(
            CreateGroup(options.Sanieren, "Sanieren-Liste", ["Nein", "Ja"], actions),
            CreateGroup(options.Eigentuemer, "Eigentuemer-Liste", fixedEigentuemerOptions, actions),
            CreateGroup(
                options.Pruefungsresultat,
                "Pruefungsresultat-Liste",
                [
                    "Pruefung bestanden",
                    "Pruefung knapp nicht bestanden",
                    "Pruefung nicht bestanden (grob undicht)",
                    "Keine"
                ],
                actions),
            CreateGroup(options.Referenzpruefung, "Referenzpruefung-Liste", ["Ja", "Nein"], actions),
            CreateGroup(
                options.EmpfohleneSanierungsmassnahmen,
                "Sanierungsmassnahmen-Liste",
                [""],
                actions),
            // Zuruecksetzen bedeutet hier: zurueck auf die reinen Katalogwerte,
            // also alle eigenen Ergaenzungen weg.
            CreateGroup(
                options.Rohrmaterial,
                "Rohrmaterial-Liste",
                PipeMaterialOptionList.FixedOptions,
                actions));
    }

    private static DropdownOptionGroupController CreateGroup(
        ObservableCollection<string> options,
        string previewTitle,
        IReadOnlyList<string> resetItems,
        DropdownOptionGroupActions actions)
        => new(
            options,
            new DropdownOptionGroupSettings(previewTitle, resetItems),
            actions);
}
