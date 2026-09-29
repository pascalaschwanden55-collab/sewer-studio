using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.UI.Player;
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

    /// <summary>
    /// Aufgabe 17, Fix-Runde 1: Die F1-Tastenübersicht (<c>ShortcutOverlay</c>-Block im
    /// PlayerWindow) und die Kürzelbeschreibungen aus <see cref="PlayerKeyboardShortcutPolicy"/>
    /// sind Pascals sichtbare Hilfetexte im Player — beide müssen deutsch bleiben. Ein
    /// wörtliches englisches Bedienwort wie "Play"/"Stop"/"Seek"/"Frame"/"Step"/"Snapshot"/
    /// "Zoom"/"Mute" als ganzes Wort ist ein Rückfall in unübersetzten Text. "Pause" ist
    /// ausdrücklich erlaubt (gleiches Wort in beiden Sprachen); Tastennamen ("Strg",
    /// "Umschalt", "Leertaste", "Esc", Pfeiltasten) sind ohnehin keine der geprüften Wörter.
    /// </summary>
    private static readonly string[] EnglischeRestwoerter =
        ["Play", "Stop", "Seek", "Frame", "Step", "Snapshot", "Zoom", "Mute"];

    private static readonly Regex EnglischesWort = new(
        @"\b(" + string.Join("|", EnglischeRestwoerter.Select(Regex.Escape)) + @")\b",
        RegexOptions.Compiled);

    [Fact]
    public void Tastenuebersicht_im_Overlay_enthaelt_keine_englischen_Restwoerter()
    {
        var xaml = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml"));

        var overlayStart = xaml.IndexOf("x:Name=\"ShortcutOverlay\"", System.StringComparison.Ordinal);
        Assert.True(overlayStart >= 0, "ShortcutOverlay nicht gefunden — Waechter pruefte sonst nichts.");
        var overlayEnde = xaml.IndexOf("<!-- Ende ShortcutOverlay -->", overlayStart, System.StringComparison.Ordinal);
        var overlayBlock = overlayEnde > overlayStart
            ? xaml[overlayStart..overlayEnde]
            : xaml[overlayStart..];

        var texte = Regex.Matches(overlayBlock, "Text=\"([^\"]*)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(texte);

        var funde = texte
            .SelectMany(text => EnglischesWort.Matches(text).Select(m => $"\"{text}\": \"{m.Value}\""))
            .ToList();

        Assert.True(funde.Count == 0,
            "Englisches Restwort im F1-Overlay:\n" + string.Join("\n", funde));
    }

    [Fact]
    public void PlayerKeyboardShortcutPolicy_Beschreibungen_enthalten_keine_englischen_Restwoerter()
    {
        var funde = PlayerKeyboardShortcutPolicy.Beschreibungen
            .SelectMany(b => EnglischesWort.Matches(b.Beschreibung)
                .Select(m => $"\"{b.Beschreibung}\": \"{m.Value}\""))
            .ToList();

        Assert.True(funde.Count == 0,
            "Englisches Restwort in PlayerKeyboardShortcutPolicy.Beschreibungen:\n" + string.Join("\n", funde));
    }
}
