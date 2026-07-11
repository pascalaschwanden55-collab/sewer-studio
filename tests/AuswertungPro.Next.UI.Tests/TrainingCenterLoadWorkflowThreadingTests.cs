using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Regressionstest zu APP-A5BD1B09: Der Fälle-Ladeworkflow orchestriert UI-Callbacks
/// (ObservableCollection-Ersatz, RootFolder-Mutation) und darf deshalb den
/// Aufrufer-Kontext (UI-Thread) NICHT abwerfen — sonst wirft die CollectionView
/// "keine Aenderungen der SourceCollection ... ausserhalb des Dispatcher-Threads".
/// </summary>
public sealed class TrainingCenterLoadWorkflowThreadingTests
{
    [Fact]
    public void Ui_callbacks_laufen_auf_dem_aufrufer_kontext_auch_bei_echtem_async_laden()
    {
        var kontext = new PumpenKontext();
        var vorher = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(kontext);
        try
        {
            SynchronizationContext? replaceCasesKontext = null;
            SynchronizationContext? statusKontext = null;

            var request = new TrainingCenterLoadWorkflowRequest(
                LoadStateAsync: async () =>
                {
                    // ECHT asynchron (nicht sofort fertig) — genau dann schlaegt
                    // ConfigureAwait(false) auf einen ThreadPool-Thread um.
                    await Task.Delay(25);
                    return new TrainingCenterState();
                },
                RootFolders: new List<string>(),
                DirectoryExists: _ => false,
                ReplaceCases: _ => replaceCasesKontext = SynchronizationContext.Current,
                UpdateRootFolderDisplay: () => { },
                SetStatusText: _ => statusKontext = SynchronizationContext.Current,
                LoadSamplesAsync: () => Task.CompletedTask,
                RefreshKbStatusAsync: () => Task.CompletedTask,
                LoadLastMatchRateAsync: () => Task.CompletedTask);

            var lauf = TrainingCenterLoadWorkflow.RunAsync(request);
            kontext.PumpeBis(lauf, TimeSpan.FromSeconds(5));

            Assert.Same(kontext, replaceCasesKontext);
            Assert.Same(kontext, statusKontext);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(vorher);
        }
    }

    /// <summary>Minimaler Single-Thread-Kontext: Posts landen in einer Queue und werden
    /// im Testthread abgearbeitet — wie ein Dispatcher, nur ohne WPF.</summary>
    private sealed class PumpenKontext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public void PumpeBis(Task task, TimeSpan timeout)
        {
            var ende = DateTime.UtcNow + timeout;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > ende)
                    throw new TimeoutException("Workflow wurde nicht fertig — Pumpe abgebrochen.");

                if (_queue.TryTake(out var eintrag, 25))
                {
                    SetSynchronizationContext(this);
                    eintrag.Callback(eintrag.State);
                }
            }

            task.GetAwaiter().GetResult(); // Ausnahmen des Workflows sichtbar machen
        }
    }
}
