using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter aus Aufgabe 12 des Optik-Plans (28.09.2026, «Feste Farben und Schriften auf
/// Tokens»): feste Farben und die Schriftart "Consolas" gehoeren ausserhalb der Video-Fenster
/// auf Theme-Tokens, damit der Dunkelmodus stimmt. Ein Ersatzwert fuer den Fall ohne laufende
/// Anwendung (Unit-Test) bleibt erlaubt — er greift erst, wenn TryFindResource/SetResourceReference
/// nichts liefert.
/// </summary>
public sealed class DesignAuditOptikTokenTests
{
    private static readonly string UiRoot = RepoFile("src", "AuswertungPro.Next.UI");

    /// <summary>
    /// Erlaubt ist "Consolas" nur an drei Stellen: die FontMono-Tokendefinition selbst
    /// (Theme/Controls.xaml) und die bekannten Rueckfallausdruecke
    /// <c>TryFindResource("FontMono") ?? new FontFamily("Consolas")</c> bzw.
    /// <c>_fontMono ?? new FontFamily("Consolas")</c>, die ohne laufende Anwendung greifen.
    /// Jede weitere Fundstelle ist eine feste Schriftart, die den Dunkelmodus nicht mitmacht.
    /// </summary>
    [Fact]
    public void Kein_Consolas_ausserhalb_des_FontMono_Tokens()
    {
        var erlaubtProZeile = new Regex(
            "x:Key=\"FontMono\"|TryFindResource\\(\"FontMono\"\\)|_fontMono\\s*\\?\\?",
            RegexOptions.Compiled);
        var treffer = new List<string>();

        foreach (var datei in AlleQuellDateien())
        {
            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i++)
            {
                if (!zeilen[i].Contains("Consolas", StringComparison.Ordinal))
                    continue;
                if (erlaubtProZeile.IsMatch(zeilen[i]))
                    continue;

                treffer.Add($"{Relativ(datei)}:{i + 1}: {zeilen[i].Trim()}");
            }
        }

        Assert.True(
            treffer.Count == 0,
            "\"Consolas\" ausserhalb des FontMono-Tokens bzw. seiner Rueckfallausdruecke — bitte "
            + "{DynamicResource FontMono} (XAML) oder TryFindResource(\"FontMono\")/"
            + "SetResourceReference (C#) verwenden:\n" + string.Join("\n", treffer));
    }

    /// <summary>
    /// Regressionswaechter: HydraulikPanelWindow zeichnete den Rohrquerschnitt frueher mit 17
    /// statischen, im Konstruktor gefrorenen Pinseln (Zeilen 16-34) — nie themefaehig. Jetzt
    /// werden alle Farben je Zeichnung ueber SetResourceReference/ResolveColor aufgeloest.
    /// </summary>
    [Fact]
    public void HydraulikPanelWindow_friert_keine_statischen_Pinsel_mehr_ein()
    {
        var text = File.ReadAllText(Path.Combine(UiRoot, "Views", "Windows", "HydraulikPanelWindow.xaml.cs"));

        Assert.DoesNotContain("private static readonly SolidColorBrush", text, StringComparison.Ordinal);
        Assert.DoesNotContain("static HydraulikPanelWindow()", text, StringComparison.Ordinal);
        Assert.DoesNotContain("FontFamily = ConsolasFont", text, StringComparison.Ordinal);
        Assert.Contains("SetResourceReference(", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regressionswaechter: Die fuenf Hex-Werte und die zwei "White"-Vorgaben aus
    /// SanierungsmassnahmenWindow.xaml (Konsistenz-Kontrolle, Uebertrag-Markierung) sind auf
    /// Theme-Tokens umgestellt.
    /// </summary>
    [Fact]
    public void SanierungsmassnahmenWindow_xaml_hat_keine_Hex_Ersatzfarben_mehr()
    {
        var text = File.ReadAllText(Path.Combine(UiRoot, "Views", "Windows", "SanierungsmassnahmenWindow.xaml"));

        foreach (var alterWert in new[] { "#33FF4444", "#33FF8C00", "\"#FF8C00\"", "#D7F5DD", "#0F3D1F" })
            Assert.DoesNotContain(alterWert, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Foreground=\"White\"", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regressionswaechter: Die beiden Aufmerksamkeits-Blitze (Randlinie, Zeile) in
    /// SanierungsmassnahmenWindow.xaml.cs verwendeten ein festes Cyan/Blau statt der Akzentfarbe
    /// des aktiven Theme.
    /// </summary>
    [Fact]
    public void SanierungsmassnahmenWindow_Blitz_folgt_der_Akzentfarbe()
    {
        var text = File.ReadAllText(Path.Combine(UiRoot, "Views", "Windows", "SanierungsmassnahmenWindow.xaml.cs"));

        Assert.DoesNotContain("Color.FromRgb(0x00, 0xDD, 0xFF)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromArgb(0xCC, 0x25, 0x63, 0xEB)", text, StringComparison.Ordinal);
        Assert.Contains("ResolveAccentBrush(", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regressionswaechter: Konfidenz-/Status-/Zonenfarben kamen frueher als zehn direkt
    /// konstruierte SolidColorBrush-Werte ohne Themebezug.
    /// </summary>
    [Fact]
    public void CodingSessionViewModel_konstruiert_keine_Pinsel_mehr_direkt()
    {
        var text = File.ReadAllText(Path.Combine(UiRoot, "ViewModels", "Windows", "CodingSessionViewModel.cs"));

        Assert.DoesNotContain("new SolidColorBrush(Color.FromRgb(", text, StringComparison.Ordinal);
        Assert.Contains("ResolveThemeBrush(", text, StringComparison.Ordinal);
    }

    /// <summary>Regressionswaechter: die anfaengliche Bereitschaftsfarbe war fest grau.</summary>
    [Fact]
    public void TrainingCenterViewModel_Bereitschaftsfarbe_kommt_aus_dem_Theme()
    {
        var text = File.ReadAllText(Path.Combine(UiRoot, "ViewModels", "Windows", "TrainingCenterViewModel.cs"));

        Assert.Contains("Application.Current?.TryFindResource(\"MutedBrush\")", text, StringComparison.Ordinal);
    }

    /// <summary>Regressionswaechter: die vier Bereitschaftsfarben der Wissensdatenbank kamen frueher fest aus Rgb(...) ohne Themebezug.</summary>
    [Fact]
    public void TrainingKnowledgeBaseStatusPresentationBuilder_nutzt_Theme_Tokens()
    {
        var text = File.ReadAllText(Path.Combine(
            UiRoot, "Ai", "Training", "TrainingKnowledgeBaseStatusPresentationBuilder.cs"));

        foreach (var token in new[] { "SuccessBrush", "WarningBrush", "DangerBrush", "MutedBrush" })
            Assert.Contains($"ResolveBrush(\"{token}\"", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regressionswaechter: Hover-/Press-Farben und der Code-Schriftgrad des VSA-Code-Explorers
    /// waren fest (helles Blau, Consolas) statt aus dem Theme.
    /// </summary>
    [Fact]
    public void VsaCodeExplorerColumnTileRenderer_hat_keine_festen_Ersatzfarben_mehr()
    {
        var text = File.ReadAllText(Path.Combine(UiRoot, "Ai", "Vsa", "VsaCodeExplorerColumnTileRenderer.cs"));

        Assert.DoesNotContain("ColorConverter.ConvertFromString(\"#F0F4FF\")", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ColorConverter.ConvertFromString(\"#E0EAFF\")", text, StringComparison.Ordinal);
        Assert.DoesNotContain("private static readonly FontFamily ConsolasFont", text, StringComparison.Ordinal);
        Assert.Contains("findResource(\"AccentSubtleBrush\")", text, StringComparison.Ordinal);
        Assert.Contains("findResource(\"SelectionBackgroundBrush\")", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regressionswaechter: Die Badge-Ersatzfarben des VSA-Code-Explorer-Presenters waren feste
    /// Hex-Werte statt Token-Namen, die der Renderer gegen das aktive Theme aufloest.
    /// </summary>
    [Fact]
    public void VsaCodeExplorerColumnTilePresenter_liefert_Token_statt_Hex_als_Ersatzfarbe()
    {
        var text = File.ReadAllText(Path.Combine(UiRoot, "Ai", "Vsa", "VsaCodeExplorerColumnTilePresenter.cs"));

        Assert.DoesNotContain("\"#2563EB\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"#16A34A\"", text, StringComparison.Ordinal);
        Assert.Contains("?? \"AccentBrush\"", text, StringComparison.Ordinal);
        Assert.Contains("\"End\", \"SuccessBrush\"", text, StringComparison.Ordinal);
    }

    private static IEnumerable<string> AlleQuellDateien()
        => Directory.EnumerateFiles(UiRoot, "*.xaml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(UiRoot, "*.cs", SearchOption.AllDirectories))
            .Where(d => !IstBuildAusgabe(d));

    private static bool IstBuildAusgabe(string pfad)
        => pfad.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || pfad.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string Relativ(string pfad) => Path.GetRelativePath(UiRoot, pfad);
}
