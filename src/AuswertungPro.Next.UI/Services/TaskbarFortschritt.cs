using System;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13 (Windows-Integration). Traegt <see cref="TaskbarItemInfo"/>
/// am Hauptfenster nach, statt es fest in MainWindow.xaml zu verdrahten - so bleibt das Fenster
/// unveraendert und funktioniert auch, solange noch kein Fortschritt gemeldet wurde.
///
/// Das Fenster wird erst bei jedem Aufruf ueber den Resolver geholt (nicht einmalig im
/// Konstruktor gebunden): Der zentrale <see cref="AuswertungPro.Next.UI.ServiceProvider"/> baut
/// die Dienste, bevor <c>MainWindow</c> ueberhaupt erzeugt ist.
///
/// Fix-Runde 1 (29.09.2026): Aufrufer wie die Datensicherung melden Fortschritt aus einem
/// <c>IProgress&lt;T&gt;</c>, das (anders als <see cref="System.Progress{T}"/>) NICHT selbst auf
/// den UI-Thread marshallt - der Aufruf traf bisher `Window.TaskbarItemInfo` direkt vom
/// Threadpool-Thread und warf eine `InvalidOperationException` ("falscher Thread"), die den
/// GANZEN Sicherungslauf als Fehlschlag werten liess (real reproduziert). Diese Klasse marshallt
/// deshalb selbst auf den UI-Thread (CheckAccess/BeginInvoke) und darf NIE eine Ausnahme nach
/// aussen durchreichen - eine reine Anzeige darf einen Hintergrundlauf niemals abbrechen.
/// </summary>
public sealed class TaskbarFortschritt : ITaskbarFortschritt
{
    private readonly Func<Window?> _fensterAufloesen;

    public TaskbarFortschritt(Func<Window?>? fensterAufloesen = null)
        => _fensterAufloesen = fensterAufloesen ?? (() => System.Windows.Application.Current?.MainWindow);

    public void SetzeFortschritt(double anteil)
        => Anwenden(TaskbarItemProgressState.Normal, Math.Clamp(anteil, 0d, 1d));

    public void SetzeUnbestimmt()
        => Anwenden(TaskbarItemProgressState.Indeterminate, 0d);

    public void Fehler()
        => Anwenden(TaskbarItemProgressState.Error, 1d);

    public void Beenden()
        => Anwenden(TaskbarItemProgressState.None, 0d);

    private void Anwenden(TaskbarItemProgressState zustand, double wert)
    {
        try
        {
            // Application.Current.Dispatcher ist von JEDEM Thread aus sicher lesbar (das ist der
            // Sinn von DispatcherObject.Dispatcher: er dient gerade dazu, von einem fremden
            // Thread aus zu pruefen/zu marshallen). Erst der eigentliche Fensterzugriff braucht
            // den UI-Thread.
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
                return; // keine laufende Anwendung (Kommandozeilen-/Testkontext)

            if (dispatcher.CheckAccess())
            {
                AnwendenAufUiThread(zustand, wert);
                return;
            }

            // Bewusst BeginInvoke (asynchron), nicht Invoke: der aufrufende Hintergrundthread
            // (z. B. die Datensicherung in Task.Run) darf durch eine blosse Anzeige nie
            // blockiert werden.
            dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(() => AnwendenAufUiThread(zustand, wert)));
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"Taskleisten-Fortschritt konnte nicht gesetzt werden: {ex.Message}");
        }
    }

    private void AnwendenAufUiThread(TaskbarItemProgressState zustand, double wert)
    {
        try
        {
            var fenster = _fensterAufloesen();
            if (fenster is null)
                return;

            var info = fenster.TaskbarItemInfo;
            if (info is null)
            {
                info = new TaskbarItemInfo();
                fenster.TaskbarItemInfo = info;
            }

            info.ProgressState = zustand;
            info.ProgressValue = wert;
        }
        catch (Exception ex)
        {
            // Laeuft ueber BeginInvoke spaeter/asynchron - eine hier unbehandelte Ausnahme
            // wuerde sonst als unbehandelte Dispatcher-Ausnahme im ganzen Programm auftauchen.
            BestEffort.ReportWarning($"Taskleisten-Fortschritt konnte nicht angewendet werden: {ex.Message}");
        }
    }
}
