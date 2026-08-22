using System.IO;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ThemeResourceArchitectureTests
{
    private static readonly string[] ThemeBrushKeys =
    [
        "BgBrush",
        "BgLightBrush",
        "CardBrush",
        "CardGlassBrush",
        "BorderBrush",
        "BorderLightBrush",
        "HeaderBrush",
        "HeaderTextBrush",
        "TextBrush",
        "TextSecondaryBrush",
        "MutedBrush",
        "AccentBrush",
        "AccentHoverBrush",
        "AccentSubtleBrush",
        "HoverBrush",
        "SurfaceSubtleBrush",
        "OverlayBrush",
        "NavPanelBrush",
        "GlassBrush",
        "SuccessBrush",
        "DangerBrush",
        "WarningBrush",
        "InfoBrush",
        "NeonCyanBrush",
        "NeonBlueBrush",
        "NeonPinkBrush",
        "NeonPurpleBrush",
        "NeonGreenBrush",
        "NeonOrangeBrush",
        "LcarsAmberBrush",
        "LcarsPeachBrush",
        "LcarsLavenderBrush",
        "LcarsBlueBrush",
        "LcarsTanBrush"
    ];

    [Fact]
    public void PlayerWindow_resources_keep_theme_dependent_styles_in_window_scope()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Resources.xaml"),
                "BasedOn=\"{StaticResource Card}\"",
                "BasedOn=\"{StaticResource ToolbarButton}\"",
                "BasedOn=\"{StaticResource ToolbarButtonAccent}\"",
                "x:Key=\"MarkToolPopupButton\"")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerCodingSidePanel.xaml"),
                "Style=\"{StaticResource PlayerCard}\"",
                "Style=\"{StaticResource PlayerButton}\"",
                "Style=\"{StaticResource PlayerPrimaryButton}\"",
                "Style=\"{StaticResource SectionLabel}\"",
                "Style=\"{StaticResource DefectActionBtn}\"",
                "Style=\"{StaticResource StatTile}\""))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Theme-abhaengige PlayerWindow-Styles sollen im Window-Scope bleiben, nicht in ausgelagertem ResourceDictionary/SidePanel:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Theme_windows_do_not_pin_legacy_hard_coded_colors_or_shadow_global_button_styles()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "VideoAnalysisPipelineWindow.xaml"),
                "Background=\"#0C1019\"",
                "#0C1019",
                "#131825",
                "#1A2030",
                "#243049",
                "#F0F4FA",
                "#7B8DA6",
                "#94A3B8",
                "#60A5FA")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml"),
                "#1E293B",
                "#0F172A",
                "#94A3B8",
                "#64748B"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "DossierPrintDialog.xaml"),
                "#FF0D1117",
                "#E6EDF3",
                "#21262D",
                "#30363D",
                "#58A6FF",
                "#1A3A5C",
                "#8B949E",
                "#C9D1D9",
                "#238636",
                "#2EA043"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "HydraulikPrintDialog.xaml"),
                "#FF0D1117",
                "#E6EDF3",
                "#21262D",
                "#30363D",
                "#58A6FF",
                "#1A3A5C",
                "#C9D1D9",
                "#238636",
                "#2EA043"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Theme-Fenster sollen keine alten hart kodierten Farbwerte oder lokale Button-Style-Schatten behalten:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Theme_xaml_uses_dynamic_brush_resources_for_live_theme_switching()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var files = new[]
            {
                RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Controls.xaml"),
                RepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml"),
                RepoFile("src", "AuswertungPro.Next.UI", "Theme", "ThemeLight.xaml"),
                RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Theme.xaml")
            }
            .Concat(Directory.EnumerateFiles(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages"),
                "*.xaml",
                SearchOption.AllDirectories))
            .ToArray();

        var offenders = files
            .SelectMany(path => FindStaticThemeBrushResourceOffenders(uiRoot, path))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Live-Theme-XAML soll Theme-Brushes dynamisch statt statisch referenzieren:\n"
            + string.Join("\n", offenders));
    }

    private static IEnumerable<string> FindStaticThemeBrushResourceOffenders(string uiRoot, string path)
    {
        var xaml = File.ReadAllText(path);
        var relative = Path.GetRelativePath(uiRoot, path);
        foreach (var key in ThemeBrushKeys)
        {
            if (xaml.Contains($"{{StaticResource {key}}}", StringComparison.Ordinal))
                yield return $"{relative}: StaticResource {key}";
            if (xaml.Contains($"<StaticResource ResourceKey=\"{key}\"", StringComparison.Ordinal))
                yield return $"{relative}: StaticResource ResourceKey {key}";
        }
    }
}
