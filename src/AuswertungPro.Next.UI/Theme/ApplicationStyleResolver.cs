using System.Windows;

namespace AuswertungPro.Next.UI.Theme;

/// <summary>
/// Liefert den impliziten Anwendungsstil fuer Steuerelemente, die erst im Code
/// erzeugt werden. Zusatzstile koennen so auf dem aktiven Hell-/Dunkelmodus aufbauen.
/// </summary>
internal static class ApplicationStyleResolver
{
    public static Style? FindImplicit(Type controlType)
        => System.Windows.Application.Current?.TryFindResource(controlType) as Style;
}
