using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditCommandReachabilityTests
{
    [Fact]
    public void Main_menu_exposes_both_cost_catalog_editors()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml"));

        Assert.Contains("Command=\"{Binding OpenPriceCatalogCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenTemplateEditorCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Holding_context_menu_exposes_video_ai_pipeline_for_selected_record()
    {
        var xaml = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml"));

        Assert.Contains("PlacementTarget.DataContext.OpenVideoAiPipelineCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("CommandParameter=\"{Binding PlacementTarget.SelectedItem", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"KI-Videoanalyse", xaml, StringComparison.Ordinal);
    }
}
