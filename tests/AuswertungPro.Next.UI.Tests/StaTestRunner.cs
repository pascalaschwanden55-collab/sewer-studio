using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Tests;

internal static class StaTestRunner
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    public static void Run(Action action, TimeSpan? timeout = null)
    {
        ExceptionDispatchInfo? failure = null;
        // Im isolierten Kindprozess endet der STA-Thread NICHT, sondern wird geparkt und stirbt mit
        // dem Prozess. Grund (belegt an zehn Absturzabzuegen, 30.09.2026): Endet ein WPF-Thread,
        // auf dem die Windows-Texteingabedienste (TSF, msctf.dll) aktiv waren, raeumt msctf beim
        // Thread-Ende (LdrShutdownThread) auf und ruft dabei in .NET zurueck, obwohl die Laufzeit
        // den Thread schon abgebaut hat -> FailFast 0xC0000602 "Attempt to execute managed code
        // after the .NET runtime thread state has been destroyed". Beim Prozessende laufen diese
        // Thread-Aufraeumroutinen nicht mehr. Im gemeinsamen Testprozess bleibt es beim Join,
        // damit dort keine Threads liegen bleiben.
        var parken = WpfIsolatedTestProcess.IsChildProcess;
        // Bewusst nicht entsorgt: Bei einer Zeitueberschreitung ruft der Thread Set() spaeter noch auf.
        var fertig = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                fertig.Set();
            }

            if (parken)
                Thread.Sleep(Timeout.Infinite);
        })
        {
            IsBackground = true,
            Name = "WPF-STA-Test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var maximum = timeout ?? DefaultTimeout;
        Assert.True(
            parken ? fertig.Wait(maximum) : thread.Join(maximum),
            $"Der WPF-Test wurde nicht innerhalb von {maximum.TotalSeconds:0.###} Sekunden beendet. " +
            "Er kann blockiert sein oder unter hoher Systemlast zu langsam laufen.");
        failure?.Throw();
    }
}
