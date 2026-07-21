using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageViewModelDependencyTests
{
    [Fact]
    public void DataPage_haelt_Abhaengigkeiten_gezielt()
    {
        var viewModelDirectory = RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages");
        var source = string.Join(
            Environment.NewLine,
            Directory.GetFiles(viewModelDirectory, "DataPageViewModel*.cs").Select(File.ReadAllText));

        Assert.Contains("private readonly IDialogService _dialogs;", source);
        Assert.Contains("private readonly AppSettings _settings;", source);
        Assert.Contains("private readonly IProtocolPdfExporter _protocolPdfExporter;", source);
        Assert.DoesNotContain("private readonly ProtocolPdfExporter _protocolPdfExporter;", source);
        Assert.DoesNotContain("private readonly IDerivedCostFieldSynchronizer _costFieldSynchronizer;", source);
        Assert.DoesNotContain("private readonly DashboardRefreshNotifier _dashboardRefresh;", source);
        Assert.DoesNotContain("private readonly BatchMediaSearchService _batchMediaSearch;", source);
        Assert.Contains("private readonly IProtocolService _protocols;", source);
        Assert.Contains("private readonly IVideoAnalysisPipelineFactory _videoAnalysisPipelineFactory;", source);
        Assert.Contains("private readonly IDataPageSanierungViewModelFactory _sanierungViewModels;", source);
        Assert.DoesNotContain("private readonly IAiSanierungOptimizationFactory _sanierungOptimizationFactory;", source);
        Assert.Contains("private readonly IDataPageWindowLauncher _windows;", source);
        Assert.DoesNotContain("new VideoAnalysisPipelineWindow(", source);
        Assert.DoesNotContain("new MediaSearchWindow(", source);
        Assert.DoesNotContain("new HydraulikPanelWindow(", source);
        Assert.Contains("_windows.ShowVideoAnalysis", source);
        Assert.Contains("_windows.ShowMediaSearch", source);
        Assert.Contains("_windows.ShowHydraulik", source);
        Assert.Contains("private readonly IHoldingRenameService _holdingRename;", source);
        Assert.Contains("private readonly IPdfTextLayerRewriter _pdfTextLayerRewrite;", source);
        Assert.DoesNotContain("private readonly ServiceProvider _sp;", source);
        Assert.DoesNotContain("_sp.", source);
        Assert.DoesNotContain("_sp.Dialogs", source);
        Assert.DoesNotContain("_sp.Settings", source);
        Assert.DoesNotContain("_sp.MeasureRecommendation", source);
        Assert.DoesNotContain("_sp.ProtocolPdfExporter", source);
        Assert.DoesNotContain("_sp.CostFieldSync", source);
        Assert.DoesNotContain("_sp.DashboardRefresh", source);
        Assert.DoesNotContain("_sp.BatchMediaSearch", source);
        Assert.DoesNotContain("_sp.Protocols", source);
        Assert.Contains("protocolRegeneration: services.ProtocolSingleRegeneration", source);
        Assert.Contains("_holdingRename = services.HoldingRename;", source);
        Assert.Contains("_pdfTextLayerRewrite = services.PdfTextLayerRewrite;", source);
        Assert.DoesNotContain("ProtocolRegenerationService", source);

        var pageDirectory = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages");
        var pageSources = string.Join(
            Environment.NewLine,
            Directory.GetFiles(pageDirectory, "DataPage*.cs").Select(File.ReadAllText));
        Assert.DoesNotContain("Vm.Services", pageSources);
        Assert.DoesNotContain("private ServiceProvider Services", pageSources);
        Assert.DoesNotContain("Services.Settings", pageSources);
        Assert.DoesNotContain("Services.Dialogs", pageSources);
        Assert.DoesNotContain("Services.Vsa", pageSources);
        Assert.DoesNotContain("Services.CodeCatalog", pageSources);
        Assert.Contains("private IDialogService Dialogs => Vm.Dialogs;", pageSources);
        Assert.Contains("private AppSettings Settings => Vm.Settings;", pageSources);
        Assert.Contains("DataPageHoldingRenameController.Apply(", pageSources);
        Assert.Contains("vm.RemoveRecords(", pageSources);
        Assert.DoesNotContain("HoldingRenameService.Rename(", pageSources);
        Assert.DoesNotContain("vm.Project.RemoveRecord(", pageSources);
        Assert.DoesNotContain("AuswertungPro.Next.Infrastructure.HoldingFolderDistributor", pageSources);

        var renameController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "DataPage", "DataPageHoldingRenameController.cs"));
        Assert.Contains("IHoldingRenameService renameService", renameController);
        Assert.Contains("IPdfTextLayerRewriter pdfTextLayerRewrite", renameController);
        Assert.Contains("renameService.Rename(", renameController);
        Assert.Contains("pdfTextLayerRewrite.RewriteIdentifierInPlace(", renameController);

        var provider = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ServiceProvider.cs"));
        Assert.Contains("public IHoldingRenameService HoldingRename { get; }", provider);
        Assert.Contains("HoldingRename = new HoldingRenameFileService();", provider);

        var observationsWindow = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "BeobachtungenWindow.xaml.cs"));
        Assert.DoesNotContain("private readonly ServiceProvider _services;", observationsWindow);
        Assert.Contains("private readonly AppSettings _settings;", observationsWindow);
    }
}
