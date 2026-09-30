using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]
[Collection("IsolatedWpf")]
public sealed class PipelineCompletionWindowTests
{
    [Fact]
    public async Task Abschluss_zeigt_Warnung_und_erreichbaren_letzten_Befund_im_echten_Fenster()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(PipelineCompletionWindowTests).FullName + "." + nameof(Kindprozess_prueft_Abschluss),
            TimeSpan.FromSeconds(40));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [Fact]
    public async Task Abbruch_setzt_die_Taskleiste_nicht_auf_rot_im_echten_Fenster()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(PipelineCompletionWindowTests).FullName + "." + nameof(Kindprozess_prueft_Abbruch),
            TimeSpan.FromSeconds(40));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_prueft_Abbruch()
    {
        // Fix-Runde 1, MINOR 5: Vm.SetError("Abgebrochen.") setzt HasError=true wie ein echter
        // Fehler (unveraendertes Fehlerbanner-Verhalten) - die Taskleiste darf einen
        // Benutzerabbruch trotzdem nicht rot zeigen.
        StaTestRunner.Run(() =>
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            var taskbar = new FakeTaskbarFortschritt();
            var window = new VideoAnalysisPipelineWindow(
                new PipelineRequest("Test", "synthetisch.mp4", []),
                new CancelledPipeline(),
                taskbar)
            {
                ShowActivated = false, ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -10_000, Top = -10_000
            };
            try
            {
                window.Show();
                ((Button)window.FindName("StartButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
                Assert.True(window.Vm.HasError);
                Assert.Equal("Abgebrochen.", window.Vm.ErrorText);
                Assert.DoesNotContain("fehler", taskbar.Aufrufe);
                Assert.Equal("beenden", taskbar.Aufrufe[^1]);
            }
            finally
            {
                window.Close();
            }
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    [IsolatedWpfFact]
    public void Kindprozess_prueft_Abschluss()
    {
        StaTestRunner.Run(() =>
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            var detections = Enumerable.Range(0, 1001)
                .Select(i => new RawVideoDetection($"Befund {i}", i, i, "mid")).ToArray();
            var result = new PipelineResult(null, detections, [], null,
                ["SAM: Masken gingen verloren."], null, Incomplete: true);
            var window = new VideoAnalysisPipelineWindow(new PipelineRequest("Test", "synthetisch.mp4", []),
                new CompletedPipeline(result))
            {
                ShowActivated = false, ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -10_000, Top = -10_000
            };
            try
            {
                window.Show();
                ((Button)window.FindName("StartButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
                Assert.True(window.Vm.IsDone);
                Assert.Equal("Unvollständig", window.Vm.PhaseLabel);
                var warning = (Border)window.FindName("ResultWarningsPanel");
                Assert.True(warning.IsVisible);
                Assert.Contains("SAM: Masken gingen verloren.", ((TextBlock)window.FindName("ResultWarningsText")).Text);
                var list = (ListBox)window.FindName("DetectionsList");
                Assert.Equal(1001, list.Items.Count);
                Assert.True(VirtualizingPanel.GetIsVirtualizing(list));
                list.ScrollIntoView(list.Items[^1]);
                window.UpdateLayout();
                Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(1000));
            }
            finally
            {
                window.Close();
            }
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private sealed class CompletedPipeline(PipelineResult result) : IVideoAnalysisPipelineService
    {
        public Task<PipelineResult> RunAsync(PipelineRequest request, IProgress<PipelineProgress>? progress = null,
            CancellationToken ct = default) => Task.FromResult(result);
    }

    private sealed class CancelledPipeline : IVideoAnalysisPipelineService
    {
        public Task<PipelineResult> RunAsync(PipelineRequest request, IProgress<PipelineProgress>? progress = null,
            CancellationToken ct = default) => Task.FromCanceled<PipelineResult>(new CancellationToken(true));
    }
}
