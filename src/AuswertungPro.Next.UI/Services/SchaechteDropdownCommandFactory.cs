using System.Collections.ObjectModel;

namespace AuswertungPro.Next.UI.Services;

internal sealed record SchaechteDropdownOptionCollections(
    ObservableCollection<string> Sanieren,
    ObservableCollection<string> Eigentuemer,
    ObservableCollection<string> Pruefungsresultat,
    ObservableCollection<string> Referenzpruefung);

internal sealed record SchaechteDropdownCommands(
    DropdownCommandGroup Sanieren,
    DropdownCommandGroup Eigentuemer,
    DropdownCommandGroup Pruefungsresultat,
    DropdownCommandGroup Referenzpruefung);

internal static class SchaechteDropdownCommandFactory
{
    internal static SchaechteDropdownCommands Create(
        SchaechteDropdownOptionCollections options,
        IReadOnlyList<string> fixedEigentuemerOptions,
        DropdownOptionGroupActions actions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fixedEigentuemerOptions);
        ArgumentNullException.ThrowIfNull(actions);

        return new SchaechteDropdownCommands(
            CreateGroup(
                options.Sanieren,
                new DropdownOptionGroupSettings("Sanieren-Liste", ["Nein", "Ja"]),
                actions),
            CreateGroup(
                options.Eigentuemer,
                new DropdownOptionGroupSettings(
                    "Eigentümer-Liste",
                    fixedEigentuemerOptions,
                    LockedToResetItems: true),
                actions),
            CreateGroup(
                options.Pruefungsresultat,
                new DropdownOptionGroupSettings(
                    "Prüfungsresultat-Liste",
                    [
                        // Datenwert, kein Anzeigetext: identisch zu ExcelReportStyle.Farbregeln
                        // und DataPageDropdownOptionGroupFactory. Bewusst ae/oe statt Umlaut,
                        // damit gespeicherte Projektwerte weiterhin dieselbe Farbe treffen.
                        "Pruefung bestanden",
                        "Pruefung knapp nicht bestanden",
                        "Pruefung nicht bestanden (grob undicht)",
                        "Keine"
                    ]),
                actions),
            CreateGroup(
                options.Referenzpruefung,
                new DropdownOptionGroupSettings("Referenzprüfung-Liste", ["Ja", "Nein"]),
                actions));
    }

    private static DropdownCommandGroup CreateGroup(
        ObservableCollection<string> options,
        DropdownOptionGroupSettings settings,
        DropdownOptionGroupActions actions)
    {
        var controller = new DropdownOptionGroupController(options, settings, actions);
        return DropdownCommandFactory.Create(new DropdownCommandActions(
            controller.Edit,
            controller.Preview,
            controller.Reset,
            controller.Add,
            controller.Remove));
    }
}
