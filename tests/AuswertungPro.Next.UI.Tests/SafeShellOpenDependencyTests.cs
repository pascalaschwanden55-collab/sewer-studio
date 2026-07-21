using System.Reflection;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Common;
using AuswertungPro.Next.UI.Services;
using Microsoft.Extensions.Logging;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SafeShellOpenDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_den_sicheren_Oeffnungsdienst_direkt()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.ShellOpen,
            services.GetService(typeof(ISafeShellOpenService)));
    }

    [Fact]
    public void KompatibilitaetsFassade_kann_den_Dienst_nicht_mehr_global_austauschen()
    {
        var before = SafeShellOpen.CompatibilityService;
        var use = typeof(SafeShellOpen).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(
            () => use.Invoke(null, [new SafeShellOpenService()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, SafeShellOpen.CompatibilityService);
    }

    [Fact]
    public void DataPage_und_SchaechtePage_oeffnen_Dateien_ueber_den_injizierten_Dienst()
    {
        var dataViewModel = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));
        var shaftViewModel = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "SchaechtePageViewModel.cs"));
        var builderViewModel = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages"),
                    "BuilderPageViewModel*.cs")
                .Select(File.ReadAllText));
        var printController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "DataPage", "DataPagePrintController.cs"));
        var dataPage = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var shaftPage = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechtePage.xaml.cs"));
        var shaftFileActions = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "DataPage", "SchaechteFileActionController.cs"));

        Assert.Contains("private readonly ISafeShellOpenService _shellOpen;", dataViewModel);
        Assert.Contains("_shellOpen = services.ShellOpen;", dataViewModel);
        Assert.Contains("internal ISafeShellOpenService ShellOpen => _shellOpen;", dataViewModel);
        Assert.Contains("openPdf: path => TryOpenFile(path).Success", dataViewModel);
        Assert.Contains("TryOpenFile);", dataViewModel);
        Assert.Contains("Vm.ShellOpen.TryOpen(", dataPage);

        Assert.Contains("private readonly ISafeShellOpenService _shellOpen;", shaftViewModel);
        Assert.Contains("shellOpen: services.ShellOpen", shaftViewModel);
        Assert.Contains("_shellOpen = shellOpen ?? throw", shaftViewModel);
        Assert.Contains("internal ISafeShellOpenService ShellOpen => _shellOpen;", shaftViewModel);
        Assert.Contains("viewModel.ShellOpen,", shaftPage);
        Assert.Contains("_shellOpen = shellOpen ?? throw", shaftFileActions);
        Assert.Contains("_shellOpen.TryOpen(", shaftFileActions);
        Assert.DoesNotContain("Vm.ShellOpen.TryOpen(", shaftPage);

        Assert.DoesNotContain("SafeShellOpen.TryOpen", dataViewModel);
        Assert.DoesNotContain("SafeShellOpen.TryOpen", dataPage);
        Assert.DoesNotContain("SafeShellOpen.TryOpen", shaftViewModel);
        Assert.DoesNotContain("SafeShellOpen.TryOpen", shaftPage);
        Assert.DoesNotContain("SafeShellOpen.TryOpen", shaftFileActions);

        Assert.Contains("private readonly ISafeShellOpenService _shellOpen;", builderViewModel);
        Assert.Contains("shellOpen: services.ShellOpen", builderViewModel);
        Assert.Contains("_shellOpen = shellOpen ?? throw", builderViewModel);
        Assert.Contains("openPdf: path => _shellOpen.TryOpen(path, out _)", builderViewModel);
        Assert.DoesNotContain("SafeShellOpen.TryOpen", builderViewModel);

        Assert.Contains("_openPdf = openPdf ?? throw", printController);
        Assert.DoesNotContain(
            "_openPdf = openPdf ?? (path => DataPageOriginalPdfController.TryShellOpen",
            printController);
        Assert.Equal(
            CountOccurrences(printController, "[Obsolete(\"Kompatibilitaetskonstruktor."),
            CountOccurrences(printController, "DataPageOriginalPdfController.TryShellOpen"));
        Assert.Equal(
            CountOccurrences(builderViewModel, "[Obsolete("),
            CountOccurrences(builderViewModel, "SafeShellOpen.CompatibilityService"));
    }

    private static int CountOccurrences(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;
}
