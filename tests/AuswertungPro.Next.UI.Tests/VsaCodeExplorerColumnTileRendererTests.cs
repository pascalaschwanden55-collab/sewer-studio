using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Vsa;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerColumnTileRendererTests
{
    [Fact]
    public void CreateButton_baut_standard_tile_mit_balken_badge_beschreibung_und_click()
    {
        RunSta(() =>
        {
            var clicked = false;
            var tile = new TileItem { Label = "BA" };
            var groupBrush = Brushes.DarkCyan;

            var button = VsaCodeExplorerColumnTileRenderer.CreateButton(
                new VsaCodeExplorerColumnTilePresentation(
                    LabelText: "BA",
                    DescriptionText: "Rissbildung",
                    ShowDescription: true,
                    GroupColorHex: "#123456",
                    MarkerBrushRole: VsaCodeExplorerColumnTileBrushRole.Group,
                    CodeBrushRole: VsaCodeExplorerColumnTileBrushRole.Group,
                    DescriptionBrushRole: VsaCodeExplorerColumnTileBrushRole.TextSecondary,
                    Badges: [new VsaCodeExplorerColumnTileBadge("ICM", "#2563EB")],
                    ShowSelectedChrome: false,
                    ShowInvalidChrome: false,
                    InvalidTooltip: null),
                tile,
                new Style(typeof(Button)),
                CreateResources(groupBrush),
                () => clicked = true);

            Assert.Same(tile, button.Tag);
            Assert.Equal(new Thickness(0), button.Padding);
            Assert.Equal(new Thickness(2, 1, 2, 1), button.Margin);
            Assert.Same(groupBrush, FindMarkerBar(button).Background);
            var content = Assert.IsType<DockPanel>(button.Content);
            Assert.Equal("BA", FindText(content, "BA").Text);
            Assert.Equal("Rissbildung", FindText(content, "Rissbildung").Text);
            Assert.Equal("ICM", FindText(content, "ICM").Text);

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(clicked);
        });
    }

    /// <summary>
    /// Fix-Runde 1 (Review 29.09.2026): ResolveBadgeColor unterscheidet echte Fachfarben
    /// (Klartext-Hex, z. B. eine VSA-Gruppenfarbe) von Theme-Token-Namen (Ersatzfarbe ohne
    /// Fachwert). Ein echtes Hex wird immer woertlich uebernommen; ein Token-Name wird ohne
    /// laufende Application (dieser Testthread hat keine) auf den festen Rueckfallwert Akzentblau
    /// abgebildet.
    /// </summary>
    [Theory]
    [InlineData("#123456", 0x12, 0x34, 0x56)]
    [InlineData("AccentBrush", 0x25, 0x63, 0xEB)]
    [InlineData("SuccessBrush", 0x25, 0x63, 0xEB)]
    public void CreateButton_loest_Badge_Farbe_je_nach_Hex_oder_Token_auf(
        string badgeColorHexOrToken, byte expectedRed, byte expectedGreen, byte expectedBlue)
    {
        RunSta(() =>
        {
            var button = VsaCodeExplorerColumnTileRenderer.CreateButton(
                new VsaCodeExplorerColumnTilePresentation(
                    LabelText: "BA",
                    DescriptionText: "",
                    ShowDescription: false,
                    GroupColorHex: null,
                    MarkerBrushRole: VsaCodeExplorerColumnTileBrushRole.Group,
                    CodeBrushRole: VsaCodeExplorerColumnTileBrushRole.Group,
                    DescriptionBrushRole: VsaCodeExplorerColumnTileBrushRole.TextSecondary,
                    Badges: [new VsaCodeExplorerColumnTileBadge("ICM", badgeColorHexOrToken)],
                    ShowSelectedChrome: false,
                    ShowInvalidChrome: false,
                    InvalidTooltip: null),
                new TileItem { Label = "BA" },
                new Style(typeof(Button)),
                CreateResources(),
                () => { });

            var content = Assert.IsType<DockPanel>(button.Content);
            var badgeText = FindText(content, "ICM");
            var foreground = Assert.IsType<SolidColorBrush>(badgeText.Foreground);
            Assert.Equal(Color.FromRgb(expectedRed, expectedGreen, expectedBlue), foreground.Color);
        });
    }

    [Fact]
    public void CreateButton_setzt_selected_chrome_aus_presentation()
    {
        RunSta(() =>
        {
            var accentBrush = Brushes.OrangeRed;

            var button = VsaCodeExplorerColumnTileRenderer.CreateButton(
                new VsaCodeExplorerColumnTilePresentation(
                    LabelText: "BB",
                    DescriptionText: "",
                    ShowDescription: false,
                    GroupColorHex: null,
                    MarkerBrushRole: VsaCodeExplorerColumnTileBrushRole.Accent,
                    CodeBrushRole: VsaCodeExplorerColumnTileBrushRole.Accent,
                    DescriptionBrushRole: VsaCodeExplorerColumnTileBrushRole.Text,
                    Badges: [],
                    ShowSelectedChrome: true,
                    ShowInvalidChrome: false,
                    InvalidTooltip: null),
                new TileItem { Label = "BB" },
                new Style(typeof(Button)),
                CreateResources(accentBrush: accentBrush),
                () => { });

            Assert.Equal(new Thickness(2), button.BorderThickness);
            Assert.Same(accentBrush, button.BorderBrush);
            var background = Assert.IsType<SolidColorBrush>(button.Background);
            Assert.Equal(0.12, background.Opacity, precision: 2);
            Assert.Equal(Colors.DodgerBlue, background.Color);
        });
    }

    [Fact]
    public void CreateButton_setzt_invalid_chrome_und_streicht_code()
    {
        RunSta(() =>
        {
            var button = VsaCodeExplorerColumnTileRenderer.CreateButton(
                new VsaCodeExplorerColumnTilePresentation(
                    LabelText: "BC",
                    DescriptionText: "",
                    ShowDescription: false,
                    GroupColorHex: null,
                    MarkerBrushRole: VsaCodeExplorerColumnTileBrushRole.Invalid,
                    CodeBrushRole: VsaCodeExplorerColumnTileBrushRole.Invalid,
                    DescriptionBrushRole: VsaCodeExplorerColumnTileBrushRole.Invalid,
                    Badges: [],
                    ShowSelectedChrome: false,
                    ShowInvalidChrome: true,
                    InvalidTooltip: "ungültig"),
                new TileItem { Label = "BC" },
                new Style(typeof(Button)),
                CreateResources(),
                () => { });

            Assert.Equal(0.7, button.Opacity, precision: 2);
            Assert.Equal("ungültig", button.ToolTip);
            var content = Assert.IsType<DockPanel>(button.Content);
            Assert.Contains(
                FindText(content, "BC").TextDecorations,
                decoration => decoration.Location == TextDecorationLocation.Strikethrough);
        });
    }

    private static VsaCodeExplorerColumnTileRenderResources CreateResources(
        Brush? groupBrush = null,
        Brush? accentBrush = null)
        => new(
            AccentBrush: accentBrush ?? Brushes.DodgerBlue,
            TextBrush: Brushes.Black,
            TextSecondaryBrush: Brushes.Gray,
            InvalidBrush: Brushes.LightSlateGray,
            AccentColor: Colors.DodgerBlue,
            GroupBrushResolver: _ => groupBrush ?? Brushes.SeaGreen);

    private static Border FindMarkerBar(Button button)
        => Assert.IsType<Border>(Assert.IsType<DockPanel>(button.Content).Children[0]);

    private static TextBlock FindText(DependencyObject root, string text)
        => FindVisualChildren<TextBlock>(root).Single(tb => tb.Text == text);

    private static T[] FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        var result = new System.Collections.Generic.List<T>();
        Visit(root, result);
        return result.ToArray();
    }

    private static void Visit<T>(DependencyObject node, System.Collections.Generic.List<T> result)
        where T : DependencyObject
    {
        if (node is T typed)
            result.Add(typed);

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
            Visit(VisualTreeHelper.GetChild(node, i), result);
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }
}
