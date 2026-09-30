using System.Threading;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13, Fix-Runde 1 (CRITICAL 1): reproduziert den echten Fehler
/// (ein Hintergrundthread meldet Fortschritt, TaskbarFortschritt griff bisher direkt auf
/// Window.TaskbarItemInfo zu -&gt; InvalidOperationException, siehe SettingsFullBackupWorkflow -
/// FullBackupService.RunAsync ruft den Fortschritts-Callback aus Task.Run) und beweist, dass die
/// echte Klasse jetzt weder wirft noch den Zustand verliert.
/// </summary>
[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]
[Collection("IsolatedWpf")]
public sealed class TaskbarFortschrittThreadSafetyIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(TaskbarFortschrittThreadSafetyIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_Hintergrundthread_setzt_Fortschritt_ohne_Ausnahme);

    [Fact]
    public async Task TaskbarFortschritt_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_Hintergrundthread_setzt_Fortschritt_ohne_Ausnahme()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var fenster = new Window();
            var taskbar = new TaskbarFortschritt(() => fenster);

            // 1) Ein Hintergrundthread darf nie eine Ausnahme aus der Anzeige zurueckbekommen -
            //    genau das brach bisher den kompletten Sicherungslauf (real reproduziert).
            Exception? hintergrundFehler = null;
            var hintergrundThread = new Thread(() =>
            {
                try
                {
                    taskbar.SetzeFortschritt(0.5);
                }
                catch (Exception ex)
                {
                    hintergrundFehler = ex;
                }
            })
            {
                IsBackground = true,
                Name = "Taskbar-Hintergrundthread-Test",
            };
            hintergrundThread.Start();
            var beendet = hintergrundThread.Join(TimeSpan.FromSeconds(5));

            Assert.True(beendet, "Der Hintergrundthread ist nicht rechtzeitig zurueckgekehrt - " +
                "SetzeFortschritt darf NIE blockieren (kein synchrones Dispatcher.Invoke).");
            Assert.Null(hintergrundFehler);

            // 2) Der Aufruf marshallt per BeginInvoke (asynchron) - erst ein Pumpen des
            //    UI-Thread-Dispatchers (StaTestRunner pumpt selbst nicht) liefert ihn aus.
            PumpDispatcherFor(TimeSpan.FromMilliseconds(300));

            var info = Assert.IsType<TaskbarItemInfo>(fenster.TaskbarItemInfo);
            Assert.Equal(TaskbarItemProgressState.Normal, info.ProgressState);
            Assert.Equal(0.5, info.ProgressValue);

            // 3) Ein synchroner Aufruf auf dem UI-Thread selbst (CheckAccess-Zweig) funktioniert
            //    ebenso und wirft nicht.
            var ausnahme = Record.Exception(() => taskbar.Fehler());
            Assert.Null(ausnahme);
            Assert.Equal(TaskbarItemProgressState.Error, fenster.TaskbarItemInfo!.ProgressState);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static void PumpDispatcherFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
