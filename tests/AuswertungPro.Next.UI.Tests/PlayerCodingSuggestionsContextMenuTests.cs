using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Codex-Review PR #24 (P2, 01.10.2026): Ein Rechtsklick waehlt in WPF die Zeile nicht aus, die
/// Menueaktionen der KI-Vorschlaege lesen aber nur <c>LstSuggestions.SelectedItem</c>. Ohne
/// Auswahl beim Rechtsklick bestaetigte oder verwarf das Menue den zuvor markierten Vorschlag.
/// Die Ereignisliste daneben loest das schon ueber <c>CodingEventListItemSelectionHelper</c>.
/// </summary>
public sealed class PlayerCodingSuggestionsContextMenuTests
{
    [Fact]
    public void Rechtsklick_waehlt_die_Vorschlagszeile_vor_dem_Kontextmenue_aus()
    {
        var ordner = new[] { "src", "AuswertungPro.Next.UI", "Views", "Windows" };
        var xaml = File.ReadAllText(RepoFile([.. ordner, "PlayerCodingSidePanel.xaml"]));
        var code = File.ReadAllText(RepoFile([.. ordner, "PlayerCodingSidePanel.xaml.cs"]));

        var start = xaml.IndexOf("x:Name=\"LstSuggestions\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var kopf = xaml[start..xaml.IndexOf('>', start)];
        Assert.Contains("PreviewMouseRightButtonDown=\"Suggestions_PreviewMouseRightButtonDown\"", kopf);

        var handler = code.IndexOf("void Suggestions_PreviewMouseRightButtonDown(", StringComparison.Ordinal);
        Assert.True(handler >= 0);
        var zeile = code[handler..code.IndexOf('\n', handler)];
        Assert.Contains("CodingEventListItemSelectionHelper.SelectContainingListBoxItem(", zeile);
    }
}
