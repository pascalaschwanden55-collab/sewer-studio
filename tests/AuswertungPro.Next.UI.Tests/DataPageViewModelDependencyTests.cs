using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageViewModelDependencyTests
{
    [Fact]
    public void DataPage_haelt_Dialoge_und_Einstellungen_gezielt()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        Assert.Contains("private readonly IDialogService _dialogs;", source);
        Assert.Contains("private readonly AppSettings _settings;", source);
        Assert.Contains("private readonly ProtocolPdfExporter _protocolPdfExporter;", source);
        Assert.Contains("private readonly IDerivedCostFieldSynchronizer _costFieldSynchronizer;", source);
        Assert.Contains("private readonly DashboardRefreshNotifier _dashboardRefresh;", source);
        Assert.Contains("private readonly BatchMediaSearchService _batchMediaSearch;", source);
        Assert.Contains("private readonly IProtocolService _protocols;", source);
        Assert.DoesNotContain("_sp.Dialogs", source);
        Assert.DoesNotContain("_sp.Settings", source);
        Assert.DoesNotContain("_sp.MeasureRecommendation", source);
        Assert.DoesNotContain("_sp.ProtocolPdfExporter", source);
        Assert.DoesNotContain("_sp.CostFieldSync", source);
        Assert.DoesNotContain("_sp.DashboardRefresh", source);
        Assert.DoesNotContain("_sp.BatchMediaSearch", source);
        Assert.DoesNotContain("_sp.Protocols", source);

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
