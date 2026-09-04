using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Jede Player-Taste mit sichtbarem Bedienelement steht in dessen Tooltip.
/// </summary>
public sealed class DesignAuditPlayerShortcutTests
{
    [Theory]
    [InlineData("Abspielen / Pause — Leertaste")]
    [InlineData("Stopp — Taste S")]
    [InlineData("Schneller — Taste +")]
    [InlineData("Langsamer — Taste −")]
    [InlineData("5 Sekunden zurück — Pfeil links")]
    [InlineData("5 Sekunden vor — Pfeil rechts")]
    [InlineData("Erkennung ein/aus — Taste D")]
    [InlineData("Bereich markieren — Taste M")]
    [InlineData("Tastenkürzel anzeigen — F1")]
    public void Player_Knoepfe_nennen_ihre_Taste_im_Tooltip(string tooltip)
    {
        var xaml = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml"));

        Assert.Contains($"ToolTip=\"{tooltip}\"", xaml, StringComparison.Ordinal);
    }
}
