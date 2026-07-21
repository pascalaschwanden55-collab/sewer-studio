using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BeobachtungenWindowIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(BeobachtungenWindowIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_oeffnet_Fenster_mit_echten_App_Ressourcen);

    [Fact]
    public async Task Fenster_laesst_sich_in_eigenem_Wpf_Prozess_oeffnen_und_schliessen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(
            ChildTestName,
            TimeSpan.FromSeconds(30));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_oeffnet_Fenster_mit_echten_App_Ressourcen()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            app.InitializeComponent();
            Assert.NotNull(app.TryFindResource("SecondaryButton"));
            Assert.NotNull(app.TryFindResource("TextBrush"));

            var entries = new ObservableCollection<ProtocolEntry>
            {
                new()
                {
                    Code = "BAB",
                    Beschreibung = "Testbeobachtung",
                    Mpeg = "00:00:01",
                    FotoPaths = ["testfoto.jpg"]
                }
            };
            var window = new BeobachtungenWindow(
                entries,
                new AppSettings(),
                new EmptyInspectionProtocolFileLocator(),
                new RejectingShellOpenService(),
                holdingName: "Testhaltung",
                openProtocolCommand: null,
                commandParameter: null);

            OpenAndClose(window);
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static void OpenAndClose(Window window)
    {
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -10_000;
        window.Top = -10_000;

        try
        {
            window.Show();
            // Die Startanimation erzeugt fortlaufend Render-Arbeit. Loaded prueft das
            // echte Fenster, ohne auf ein dadurch unerreichbares ApplicationIdle zu warten.
            window.Dispatcher.Invoke(
                DispatcherPriority.Loaded,
                new Action(() => { }));
            window.UpdateLayout();
            Assert.True(window.IsLoaded);
            Assert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
        }

        Assert.False(window.IsVisible);
    }

    private sealed class RejectingShellOpenService : ISafeShellOpenService
    {
        public bool TryOpen(string? path, out string? error)
        {
            error = "Im Smoke-Test ist kein Shell-Aufruf erlaubt.";
            return false;
        }
    }

    private sealed class EmptyInspectionProtocolFileLocator : IInspectionProtocolFileLocator
    {
        public string? ResolveExistingPath(string? raw, string? projectPath)
            => null;

        public string? FindProtocolPath(
            HaltungRecord record,
            string? resolvedLink,
            string? initialFolder,
            string? projectPath,
            string? storedFilesRaw)
            => null;

        public List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder)
            => [];

        public void AddResolvedPdf(List<string> paths, string? raw, string projectFolder)
        {
        }

        public void ResolveSchachtPdfPaths(
            SchachtRecord schacht,
            string projectFolder,
            List<string> paths)
        {
        }
    }
}
