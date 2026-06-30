using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DropdownCommandActions(
    Action Edit,
    Action Preview,
    Action Reset,
    Action<object?> Add,
    Action<object?> Remove);

public sealed record DropdownCommandGroup(
    IRelayCommand Edit,
    IRelayCommand Preview,
    IRelayCommand Reset,
    IRelayCommand<object?> Add,
    IRelayCommand<object?> Remove);

public sealed record DataPageDropdownCommandSet(
    DropdownCommandGroup Sanieren,
    DropdownCommandGroup Eigentuemer,
    DropdownCommandGroup Pruefungsresultat,
    DropdownCommandGroup Referenzpruefung,
    DropdownCommandGroup EmpfohleneSanierungsmassnahmen);

public static class DropdownCommandFactory
{
    public static DropdownCommandGroup Create(DropdownCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.Edit);
        ArgumentNullException.ThrowIfNull(actions.Preview);
        ArgumentNullException.ThrowIfNull(actions.Reset);
        ArgumentNullException.ThrowIfNull(actions.Add);
        ArgumentNullException.ThrowIfNull(actions.Remove);

        return new DropdownCommandGroup(
            new RelayCommand(actions.Edit),
            new RelayCommand(actions.Preview),
            new RelayCommand(actions.Reset),
            new RelayCommand<object?>(actions.Add),
            new RelayCommand<object?>(actions.Remove));
    }
}

public static class DataPageDropdownCommandFactory
{
    public static DataPageDropdownCommandSet Create(
        DropdownCommandActions sanieren,
        DropdownCommandActions eigentuemer,
        DropdownCommandActions pruefungsresultat,
        DropdownCommandActions referenzpruefung,
        DropdownCommandActions empfohleneSanierungsmassnahmen)
    {
        return new DataPageDropdownCommandSet(
            DropdownCommandFactory.Create(sanieren),
            DropdownCommandFactory.Create(eigentuemer),
            DropdownCommandFactory.Create(pruefungsresultat),
            DropdownCommandFactory.Create(referenzpruefung),
            DropdownCommandFactory.Create(empfohleneSanierungsmassnahmen));
    }
}
