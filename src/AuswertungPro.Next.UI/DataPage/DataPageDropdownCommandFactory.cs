using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageDropdownCommandActions(
    Action Edit,
    Action Preview,
    Action Reset,
    Action<object?> Add,
    Action<object?> Remove);

public sealed record DataPageDropdownCommandGroup(
    IRelayCommand Edit,
    IRelayCommand Preview,
    IRelayCommand Reset,
    IRelayCommand<object?> Add,
    IRelayCommand<object?> Remove);

public sealed record DataPageDropdownCommandSet(
    DataPageDropdownCommandGroup Sanieren,
    DataPageDropdownCommandGroup Eigentuemer,
    DataPageDropdownCommandGroup Pruefungsresultat,
    DataPageDropdownCommandGroup Referenzpruefung,
    DataPageDropdownCommandGroup EmpfohleneSanierungsmassnahmen);

public static class DataPageDropdownCommandFactory
{
    public static DataPageDropdownCommandSet Create(
        DataPageDropdownCommandActions sanieren,
        DataPageDropdownCommandActions eigentuemer,
        DataPageDropdownCommandActions pruefungsresultat,
        DataPageDropdownCommandActions referenzpruefung,
        DataPageDropdownCommandActions empfohleneSanierungsmassnahmen)
    {
        return new DataPageDropdownCommandSet(
            CreateGroup(sanieren),
            CreateGroup(eigentuemer),
            CreateGroup(pruefungsresultat),
            CreateGroup(referenzpruefung),
            CreateGroup(empfohleneSanierungsmassnahmen));
    }

    private static DataPageDropdownCommandGroup CreateGroup(DataPageDropdownCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.Edit);
        ArgumentNullException.ThrowIfNull(actions.Preview);
        ArgumentNullException.ThrowIfNull(actions.Reset);
        ArgumentNullException.ThrowIfNull(actions.Add);
        ArgumentNullException.ThrowIfNull(actions.Remove);

        return new DataPageDropdownCommandGroup(
            new RelayCommand(actions.Edit),
            new RelayCommand(actions.Preview),
            new RelayCommand(actions.Reset),
            new RelayCommand<object?>(actions.Add),
            new RelayCommand<object?>(actions.Remove));
    }
}
