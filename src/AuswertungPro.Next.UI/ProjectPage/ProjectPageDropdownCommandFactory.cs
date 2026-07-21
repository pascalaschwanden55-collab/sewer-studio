using System.Collections.ObjectModel;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ProjectPage;

internal sealed record ProjectPageDropdownCommands(
    DropdownCommandGroup Sanieren,
    DropdownCommandGroup Eigentuemer);

internal static class ProjectPageDropdownCommandFactory
{
    internal static ProjectPageDropdownCommands Create(
        ObservableCollection<string> sanierenOptions,
        ObservableCollection<string> eigentuemerOptions,
        IReadOnlyList<string> fixedEigentuemerOptions,
        Func<string> getCurrentSanierenValue,
        DropdownOptionGroupActions actions)
    {
        ArgumentNullException.ThrowIfNull(sanierenOptions);
        ArgumentNullException.ThrowIfNull(eigentuemerOptions);
        ArgumentNullException.ThrowIfNull(fixedEigentuemerOptions);
        ArgumentNullException.ThrowIfNull(getCurrentSanierenValue);
        ArgumentNullException.ThrowIfNull(actions);

        var sanieren = new DropdownOptionGroupController(
            sanierenOptions,
            new DropdownOptionGroupSettings("Sanieren-Liste", ["Nein", "Ja"]),
            actions);
        var eigentuemer = new DropdownOptionGroupController(
            eigentuemerOptions,
            new DropdownOptionGroupSettings(
                "Eigentuemer-Liste",
                fixedEigentuemerOptions,
                LockedToResetItems: true),
            actions);

        return new ProjectPageDropdownCommands(
            CreateCommands(
                sanieren,
                edit: () => EditSanieren(
                    sanierenOptions,
                    getCurrentSanierenValue,
                    actions)),
            CreateCommands(
                eigentuemer,
                reset: () => ResetEigentuemer(
                    eigentuemerOptions,
                    fixedEigentuemerOptions,
                    actions.Save)));
    }

    private static void EditSanieren(
        ObservableCollection<string> options,
        Func<string> getCurrentValue,
        DropdownOptionGroupActions actions)
    {
        var result = actions.EditOptions(options.ToArray());
        if (!result.Accepted)
            return;

        DropdownOptionList.ReplaceWith(options, result.Items);
        DropdownOptionList.AddIfMissing(options, getCurrentValue());
        actions.Save();
    }

    private static void ResetEigentuemer(
        ObservableCollection<string> options,
        IReadOnlyList<string> fixedOptions,
        Action save)
    {
        DropdownOptionList.EnsureExact(options, fixedOptions);
        save();
    }

    private static DropdownCommandGroup CreateCommands(
        DropdownOptionGroupController controller,
        Action? edit = null,
        Action? reset = null)
        => DropdownCommandFactory.Create(new DropdownCommandActions(
            edit ?? new Action(controller.Edit),
            controller.Preview,
            reset ?? new Action(controller.Reset),
            controller.Add,
            controller.Remove));
}
