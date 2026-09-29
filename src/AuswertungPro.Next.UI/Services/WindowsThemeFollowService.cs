using System;
using System.Windows.Threading;
using Microsoft.Win32;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13 (Windows-Integration). Haelt die Design-Wahl "Wie
/// Windows" (<see cref="ThemeManager.System"/>) waehrend der Laufzeit aktuell: Wechselt der
/// Benutzer in den Windows-Einstellungen zwischen Hell/Dunkel, waehrend SewerStudio laeuft, wird
/// sofort neu aufgeloest und angewendet - ohne Programmneustart.
///
/// <see cref="SystemEvents.UserPreferenceChanged"/> feuert nicht garantiert auf dem
/// WPF-UI-Thread; das Anwenden der Theme-Ressourcen muss aber dort passieren (wie beim
/// bestehenden <c>ThemeManager.ThemeChanged</c>-Abonnement in <c>HydraulikPanelWindow</c>).
/// Deshalb wird ueber den uebergebenen Dispatcher marshallt.
/// </summary>
public sealed class WindowsThemeFollowService : IDisposable
{
    private readonly Func<string?> _readPreference;
    private readonly Action<string> _applyResolvedTheme;
    private readonly Dispatcher _dispatcher;
    private readonly Func<string> _readCurrentTheme;
    private readonly Func<int?> _readWindowsAppsUseLightTheme;
    private readonly object _gate = new();
    private bool _subscribed;
    private bool _disposed;
    private bool _anwendungEingereiht;

    public WindowsThemeFollowService(
        Func<string?> readPreference,
        Action<string> applyResolvedTheme,
        Dispatcher dispatcher,
        Func<string>? readCurrentTheme = null,
        Func<int?>? readWindowsAppsUseLightTheme = null)
    {
        _readPreference = readPreference ?? throw new ArgumentNullException(nameof(readPreference));
        _applyResolvedTheme = applyResolvedTheme ?? throw new ArgumentNullException(nameof(applyResolvedTheme));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        // Beide Reader sind injizierbar (Nachtrag, Schlusswelle-Item 1): fuer die Produktion
        // bleiben es dieselben statischen/Registry-Quellen wie bisher, ein Test kann sie ersetzen,
        // ohne die bestehenden drei-argumentigen Aufrufer (z. B. App.xaml.cs) anzupassen.
        _readCurrentTheme = readCurrentTheme ?? (() => ThemeManager.CurrentTheme);
        _readWindowsAppsUseLightTheme = readWindowsAppsUseLightTheme ?? WindowsThemeRegistry.ReadAppsUseLightTheme;
    }

    public void Start()
    {
        if (_subscribed || _disposed)
            return;

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _subscribed = true;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Der billige, unveraenderliche Teil (welche Kategorie ueberhaupt Hell/Dunkel betreffen
        // kann) darf schon hier auf dem SystemEvents-Thread entschieden werden - er haengt an
        // keinem Wert, der bis zum Dispatch noch veraltet sein koennte.
        if (!WindowsThemeFollowPolicy.IstRelevanteKategorie(e.Category))
            return;

        if (_dispatcher.CheckAccess())
        {
            WendeAnFallsNoetig(e.Category);
            return;
        }

        // Nachtrag (Schlusswelle-Item 1): Die eigentliche Entscheidung (SollNeuAnwenden - liest
        // die gespeicherte Praeferenz UND ThemeManager.CurrentTheme) darf NICHT mehr hier auf dem
        // SystemEvents-Thread (ein WinForms-Botschaftsfenster-Thread ausserhalb von WPF)
        // vorausberechnet und dann als fertiger Wert in die BeginInvoke-Lambda gelegt werden:
        // Zwischen dem Einreihen und der tatsaechlichen Ausfuehrung auf dem UI-Thread kann der
        // Benutzer selbst eine neuere, bewusste Design-Wahl getroffen haben (z. B. in den
        // Einstellungen fest auf Dunkel gewechselt) - eine bereits veraltete Vorab-Entscheidung
        // wuerde diese neuere Wahl sonst stillschweigend ueberschreiben. Deshalb reiht dieser
        // Zweig nur noch den Dispatch selbst ein; SollNeuAnwenden laeuft ERST in der Lambda, also
        // auf dem UI-Thread, mit dem dann aktuellen Stand.
        //
        // Mehrere kurz aufeinanderfolgende Windows-Meldungen (z. B. General UND Color fuer
        // denselben Wechsel) reihen dabei nur EINEN Dispatch ein (Coalescing): Solange ein
        // bereits eingereihter Dispatch noch nicht gelaufen ist, wird kein zweiter eingereiht -
        // der eine ausstehende Dispatch liest beim Ausfuehren ohnehin den dann aktuellen Stand.
        lock (_gate)
        {
            if (_anwendungEingereiht)
                return;
            _anwendungEingereiht = true;
        }

        _dispatcher.BeginInvoke(() =>
        {
            lock (_gate)
                _anwendungEingereiht = false;
            WendeAnFallsNoetig(e.Category);
        });
    }

    /// <summary>Liest Praeferenz, aktuelles Theme und Windows-Registry-Wert JETZT (auf dem Thread
    /// des Aufrufers - im Fremd-Thread-Zweig also erst innerhalb der Dispatcher-Lambda, also auf
    /// dem UI-Thread) und wendet nur bei einer tatsaechlich noch gueltigen Entscheidung an.</summary>
    private void WendeAnFallsNoetig(UserPreferenceCategory kategorie)
    {
        if (WindowsThemeFollowPolicy.SollNeuAnwenden(
                kategorie,
                _readPreference(),
                _readCurrentTheme(),
                _readWindowsAppsUseLightTheme,
                out var resolved))
        {
            _applyResolvedTheme(resolved);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_subscribed)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _subscribed = false;
        }

        _disposed = true;
    }
}
