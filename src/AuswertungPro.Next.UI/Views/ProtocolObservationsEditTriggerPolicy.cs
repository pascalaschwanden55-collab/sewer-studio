using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views;

/// <summary>
/// Wann die Beobachtungsliste den Bearbeitungsdialog oeffnen darf. Die reine
/// Zeilenauswahl gehoert bewusst nicht dazu: sie feuert auch bei jeder Pfeiltaste,
/// die Liste waere sonst nicht mit der Tastatur begehbar.
/// </summary>
public static class ProtocolObservationsEditTriggerPolicy
{
    public static bool OpensEditor(Key key) => key is Key.Enter;

    public static bool CanOpenEditor(bool hasSelectedEntry, bool isOpeningDialog, bool isRefreshingEntries)
        => hasSelectedEntry && !isOpeningDialog && !isRefreshingEntries;
}
