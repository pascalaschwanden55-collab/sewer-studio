using System.Windows.Media;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Zentrale Icon-Schrift. Windows 11 verwendet Segoe Fluent Icons;
/// auf aelteren Systemen greift der kompatible MDL2-Fallback.
/// </summary>
public static class IconFonts
{
    public static FontFamily Default { get; } = new("Segoe Fluent Icons, Segoe MDL2 Assets");
}
