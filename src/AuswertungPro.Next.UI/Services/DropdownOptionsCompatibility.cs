namespace AuswertungPro.Next.UI.Services;

/// <summary>Uebergang fuer bestehende oeffentliche ViewModel-Konstruktoren.</summary>
internal static class DropdownOptionsCompatibility
{
    internal static IDropdownOptionsStore Default { get; } = new FileDropdownOptionsStore();
}
