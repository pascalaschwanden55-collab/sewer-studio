using System;
using System.Windows;
using System.Windows.Shell;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13 (Windows-Integration). Traegt <see cref="TaskbarItemInfo"/>
/// am Hauptfenster nach, statt es fest in MainWindow.xaml zu verdrahten - so bleibt das Fenster
/// unveraendert und funktioniert auch, solange noch kein Fortschritt gemeldet wurde.
///
/// Das Fenster wird erst bei jedem Aufruf ueber den Resolver geholt (nicht einmalig im
/// Konstruktor gebunden): Der zentrale <see cref="AuswertungPro.Next.UI.ServiceProvider"/> baut
/// die Dienste, bevor <c>MainWindow</c> ueberhaupt erzeugt ist.
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
}
