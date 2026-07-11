using System.Windows.Media;

namespace AuswertungPro.Next.UI.Player;

/// <summary>
/// Overlay-Palette fuer Zeichnen UEBER Video/Foto (bewusst theme-unabhaengig, gesaettigt).
/// Teil der einen Farbsprache: StatusColorService.Overlay* liefert exakt diese Werte —
/// Aenderungen NUR hier vornehmen (StatusColorServiceTests sichern die Kopplung ab).
/// </summary>
public static class PlayerStatusColors
{
    public static Color Success => Color.FromRgb(0x22, 0xC5, 0x5E);

    public static Color Warning => Color.FromRgb(0xF5, 0x9E, 0x0B);

    public static Color Error => Color.FromRgb(0xEF, 0x44, 0x44);

    public static Color Muted => Color.FromRgb(0x94, 0xA3, 0xB8);

    public static Color Info => Color.FromRgb(0x3B, 0x82, 0xF6);
}
