using System.IO;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.UI.ViewModels.Pages;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DistributionTargetPreviewBuilderTests
{
    private static readonly DistributionPatternContext HaltungContext = new(
        new DateTime(2026, 7, 19),
        "Altdorf",
        "06.24341-35625");

    [Fact]
    public void Excel_builds_placeholder_path_without_directory_tree()
    {
        var result = Build(new DistributionTargetPreviewRequest(
            Root: null,
            OrdnerPattern: string.Empty,
            UnterordnerPattern: string.Empty,
            DateiPattern: "Haltungen_{Datum}",
            FixedPattern: null,
            FixedObjectFolderPattern: null,
            Extension: ".xlsx",
            ShowFilePattern: true,
            SupportsSanierung: false,
            PreviewVariant: DistributionVariant.Normal,
            SampleContext: HaltungContext));

        Assert.Equal(@"<Ziel-Wurzel>\Haltungen_20260719.xlsx", result.Vorschau);
        Assert.Empty(result.TreeNodes);
    }

    [Fact]
    public void Haltung_builds_optional_folders_pdf_and_video_at_the_same_depth()
    {
        var result = Build(new DistributionTargetPreviewRequest(
            Root: @"D:\Ziel",
            OrdnerPattern: "{Gemeinde}",
            UnterordnerPattern: "{Jahr}",
            DateiPattern: string.Empty,
            FixedPattern: "{Datum}_{Haltung}",
            FixedObjectFolderPattern: "{Haltung}",
            Extension: ".pdf",
            ShowFilePattern: false,
            SupportsSanierung: true,
            PreviewVariant: DistributionVariant.Normal,
            SampleContext: HaltungContext));

        Assert.Equal(
            @"D:\Ziel\Altdorf\2026\06.24341-35625\20260719_06.24341-35625.pdf",
            result.Vorschau);
        Assert.Equal(
            [
                new DistributionTreeNode("Altdorf", DistributionTreeNodeKind.Ordner, 0),
                new DistributionTreeNode("2026", DistributionTreeNodeKind.Ordner, 1),
                new DistributionTreeNode("06.24341-35625", DistributionTreeNodeKind.Ordner, 2),
                new DistributionTreeNode("20260719_06.24341-35625.pdf", DistributionTreeNodeKind.Pdf, 3),
                new DistributionTreeNode("20260719_06.24341-35625 (Video)", DistributionTreeNodeKind.Video, 3)
            ],
            result.TreeNodes);
    }

    [Fact]
    public void Schacht_sanierung_adds_variant_folder_without_video_node()
    {
        var context = new DistributionPatternContext(
            new DateTime(2026, 7, 19),
            "Altdorf",
            Schachtnummer: "80454");
        var result = Build(new DistributionTargetPreviewRequest(
            Root: @"C:\Ziel",
            OrdnerPattern: string.Empty,
            UnterordnerPattern: string.Empty,
            DateiPattern: string.Empty,
            FixedPattern: "{Datum}_{Schachtnummer}",
            FixedObjectFolderPattern: "{Schachtnummer}",
            Extension: ".pdf",
            ShowFilePattern: false,
            SupportsSanierung: true,
            PreviewVariant: DistributionVariant.Sanierung,
            SampleContext: context));

        Assert.Equal(
            @"C:\Ziel\80454\20260719_80454_Saniert 2026\20260719_80454.pdf",
            result.Vorschau);
        Assert.Equal(
            [
                new DistributionTreeNode("80454", DistributionTreeNodeKind.Ordner, 0),
                new DistributionTreeNode(
                    "20260719_80454_Saniert 2026",
                    DistributionTreeNodeKind.Ordner,
                    1),
                new DistributionTreeNode("20260719_80454.pdf", DistributionTreeNodeKind.Pdf, 2)
            ],
            result.TreeNodes);
        Assert.DoesNotContain(result.TreeNodes, node => node.Kind == DistributionTreeNodeKind.Video);
    }

    [Fact]
    public void Dichtheit_ignores_sanierung_preview_and_does_not_add_video_node()
    {
        var result = Build(new DistributionTargetPreviewRequest(
            Root: @"C:\Ziel",
            OrdnerPattern: string.Empty,
            UnterordnerPattern: string.Empty,
            DateiPattern: string.Empty,
            FixedPattern: "{Datum}_{Haltung}_DP",
            FixedObjectFolderPattern: "{Haltung}",
            Extension: ".pdf",
            ShowFilePattern: false,
            SupportsSanierung: false,
            PreviewVariant: DistributionVariant.Sanierung,
            SampleContext: HaltungContext));

        Assert.Equal(
            @"C:\Ziel\06.24341-35625\20260719_06.24341-35625_DP.pdf",
            result.Vorschau);
        Assert.Equal(
            [
                new DistributionTreeNode("06.24341-35625", DistributionTreeNodeKind.Ordner, 0),
                new DistributionTreeNode(
                    "20260719_06.24341-35625_DP.pdf",
                    DistributionTreeNodeKind.Pdf,
                    1)
            ],
            result.TreeNodes);
    }

    [Fact]
    public void Haltung_without_sanierung_support_stays_normal_but_keeps_video_node()
    {
        var result = Build(new DistributionTargetPreviewRequest(
            Root: @"C:\Ziel",
            OrdnerPattern: string.Empty,
            UnterordnerPattern: string.Empty,
            DateiPattern: string.Empty,
            FixedPattern: "{Datum}_{Haltung}",
            FixedObjectFolderPattern: "{Haltung}",
            Extension: ".pdf",
            ShowFilePattern: false,
            SupportsSanierung: false,
            PreviewVariant: DistributionVariant.Sanierung,
            SampleContext: HaltungContext));

        Assert.DoesNotContain("_Saniert", result.Vorschau, StringComparison.Ordinal);
        Assert.Contains(result.TreeNodes, node => node.Kind == DistributionTreeNodeKind.Video);
        Assert.DoesNotContain(
            result.TreeNodes,
            node => node.Label.Contains("_Saniert", StringComparison.Ordinal));
    }

    [Fact]
    public void Tree_labels_use_the_same_sanitized_segments_as_preview_path()
    {
        var context = HaltungContext with
        {
            Gemeinde = "Alt/dorf",
            Haltung = "06:24/341"
        };
        var result = Build(new DistributionTargetPreviewRequest(
            Root: @"C:\Ziel",
            OrdnerPattern: "{Gemeinde}",
            UnterordnerPattern: string.Empty,
            DateiPattern: string.Empty,
            FixedPattern: "{Datum}_{Haltung}",
            FixedObjectFolderPattern: "{Haltung}",
            Extension: ".pdf",
            ShowFilePattern: false,
            SupportsSanierung: false,
            PreviewVariant: DistributionVariant.Normal,
            SampleContext: context));

        Assert.Equal(
            @"C:\Ziel\Alt_dorf\06_24_341\20260719_06_24_341.pdf",
            result.Vorschau);
        Assert.Equal("Alt_dorf", result.TreeNodes[0].Label);
        Assert.Equal("06_24_341", result.TreeNodes[1].Label);
        Assert.Equal("20260719_06_24_341.pdf", result.TreeNodes[2].Label);
        Assert.Equal("20260719_06_24_341 (Video)", result.TreeNodes[3].Label);
    }

    [Fact]
    public void ViewModel_delegates_preview_and_tree_building_to_builder()
    {
        var viewModel = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DistributionTargetConfigViewModel.cs"));
        var builder = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DistributionTargetPreviewBuilder.cs"));

        Assert.Contains("DistributionTargetPreviewBuilder.Build", viewModel);
        Assert.DoesNotContain("Path.Combine", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("new DistributionTreeNode", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("private void UpdateTreeNodes", viewModel, StringComparison.Ordinal);
        Assert.Contains("IDistributionPatternResolver resolver", builder);
        Assert.Contains("IDistributionDirectoryTreeResolver directoryTreeResolver", builder);
        Assert.Contains("BuildTreeNodes", builder);
    }

    private static DistributionTargetPreviewResult Build(DistributionTargetPreviewRequest request)
    {
        var resolver = new DistributionPatternResolver();
        return DistributionTargetPreviewBuilder.Build(
            request,
            resolver,
            new DistributionDirectoryTreeResolver(resolver));
    }
}
