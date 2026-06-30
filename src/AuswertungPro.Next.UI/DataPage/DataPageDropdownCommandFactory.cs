using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageDropdownCommandSet(
    DropdownCommandGroup Sanieren,
    DropdownCommandGroup Eigentuemer,
    DropdownCommandGroup Pruefungsresultat,
    DropdownCommandGroup Referenzpruefung,
    DropdownCommandGroup EmpfohleneSanierungsmassnahmen);

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
