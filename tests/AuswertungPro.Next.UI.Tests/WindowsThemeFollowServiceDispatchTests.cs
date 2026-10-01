using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;
using Microsoft.Win32;
using AuswertungPro.Next.UI.Services;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Schlusswelle (Item 6) + Nachtrag: <see cref="AuswertungPro.Next.UI.Services.WindowsThemeFollowService"/>
/// reagiert auf <c>SystemEvents.UserPreferenceChanged</c> - das feuert auf einem WinForms-
/// Botschaftsfenster-Thread ausserhalb von WPF, NICHT auf dem UI-Thread. Ein <c>Dispatcher.Invoke</c>
/// dort wuerde diesen Systemthread synchron blockieren, bis die UI-Warteschlange den Aufruf
/// abgearbeitet hat (bei einem beschaeftigten UI-Thread haelt das den Windows-Benachrichtigungsweg
/// unnoetig auf). <c>Dispatcher.BeginInvoke</c> reiht nur ein.
///
/// NACHTRAG: Die Schlusswelle-Fassung berechnete die eigentliche Entscheidung
/// (<c>WindowsThemeFollowPolicy.SollNeuAnwenden</c> - liest Praeferenz UND <c>ThemeManager.CurrentTheme</c>)
/// noch VOR dem Dispatch auf dem Fremd-Thread und legte nur den fertigen aufgeloesten Wert in die
/// <c>BeginInvoke</c>-Lambda. Zwischen dem Einreihen und der Ausfuehrung auf dem UI-Thread kann der
/// Benutzer aber selbst eine neuere, bewusste Design-Wahl getroffen haben - eine solche veraltete
/// Vorab-Entscheidung wuerde diese neuere Wahl stillschweigend ueberschreiben. Die Entscheidung
/// wird deshalb jetzt ERST in der Lambda getroffen (also auf dem UI-Thread, mit dem dann aktuellen
/// Stand); der Fremd-Thread-Zweig reiht ausserdem nur EINEN ausstehenden Dispatch ein (Coalescing).
///
/// Ein deterministischer End-to-End-Test scheitert an zwei nicht mockbaren globalen
/// Abhaengigkeiten der Produktionsverdrahtung: dem realen Windows-Registry-Wert
/// (<c>WindowsThemeRegistry.ReadAppsUseLightTheme</c>) und dem statischen
/// <c>ThemeManager.CurrentTheme</c>. Der Dienst nimmt dafuer seit dem Nachtrag zwei optionale
/// Reader-Delegates entgegen, die genau diese beiden Quellen ersetzen (Produktions-Standardwerte
/// unveraendert, bestehende drei-argumentige Aufrufer wie <c>App.xaml.cs</c> bleiben kompatibel) -
/// damit sind unten echte Verhaltenstests moeglich, die einen echten <c>Dispatcher</c>
/// (<c>Dispatcher.CurrentDispatcher</c> auf einem eigenen STA-Thread, kein <c>new App()</c> und kein
/// produktiver Dispatcher-Pumpvorgang) verwenden.
/// </summary>
public sealed class WindowsThemeFollowServiceDispatchTests
{
    private static string Quelle()
        // Zeilenenden vereinheitlichen: Die CI checkt mit CRLF aus, die Suchmuster verwenden LF.
        => File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Services", "WindowsThemeFollowService.cs"))
            .Replace("\r\n", "\n");

    [Fact]
    public void Der_nicht_ui_thread_zweig_verwendet_BeginInvoke_statt_Invoke()
    {
        var quelle = Quelle();
        var methodenStart = quelle.IndexOf("private void OnUserPreferenceChanged", StringComparison.Ordinal);
        Assert.True(methodenStart >= 0, "OnUserPreferenceChanged wurde nicht gefunden.");
        var methodenEnde = quelle.IndexOf("\n    private void WendeAnFallsNoetig", methodenStart, StringComparison.Ordinal);
        Assert.True(methodenEnde > methodenStart, "Das Ende von OnUserPreferenceChanged wurde nicht gefunden.");
        var methode = quelle[methodenStart..methodenEnde];

        Assert.Contains("_dispatcher.BeginInvoke(", methode);
        Assert.DoesNotContain("_dispatcher.Invoke(", methode);
    }

    /// <summary>Der UI-Thread-Zweig (bereits auf dem richtigen Thread) bleibt ein DIREKTER,
    /// synchroner Aufruf ohne Umweg ueber den Dispatcher — nur der Fremd-Thread-Fall wechselt auf
    /// BeginInvoke.</summary>
    [Fact]
    public void Der_ui_thread_zweig_ruft_weiterhin_direkt_auf()
    {
        var methode = Quelle();
        Assert.Contains("if (_dispatcher.CheckAccess())\n        {\n            WendeAnFallsNoetig(e.Category);", methode);
    }

    /// <summary>
    /// NEUE REGEL (Nachtrag): <c>OnUserPreferenceChanged</c> selbst darf
    /// <c>WindowsThemeFollowPolicy.SollNeuAnwenden</c> NICHT mehr aufrufen - das geschieht
    /// ausschliesslich in <c>WendeAnFallsNoetig</c>, die im Fremd-Thread-Fall erst INNERHALB der
    /// <c>BeginInvoke</c>-Lambda laeuft. Die alte Reihenfolge (Entscheidung VOR dem Dispatch) ist
    /// damit strukturell ausgeschlossen.
    /// </summary>
    [Fact]
    public void Die_Entscheidung_faellt_nicht_mehr_im_OnUserPreferenceChanged_selbst()
    {
        var quelle = Quelle();
        var methodenStart = quelle.IndexOf("private void OnUserPreferenceChanged", StringComparison.Ordinal);
        Assert.True(methodenStart >= 0, "OnUserPreferenceChanged wurde nicht gefunden.");
        var methodenEnde = quelle.IndexOf("\n    private void WendeAnFallsNoetig", methodenStart, StringComparison.Ordinal);
        Assert.True(methodenEnde > methodenStart, "Das Ende von OnUserPreferenceChanged wurde nicht gefunden.");
        var methode = quelle[methodenStart..methodenEnde];

        Assert.DoesNotContain("WindowsThemeFollowPolicy.SollNeuAnwenden(", methode);

        var wendeAnStart = quelle.IndexOf("private void WendeAnFallsNoetig", StringComparison.Ordinal);
        Assert.True(wendeAnStart >= 0, "WendeAnFallsNoetig wurde nicht gefunden.");
        var wendeAnMethode = quelle[wendeAnStart..quelle.IndexOf("\n    public void Dispose()", wendeAnStart, StringComparison.Ordinal)];
        Assert.Contains("WindowsThemeFollowPolicy.SollNeuAnwenden(", wendeAnMethode);
    }

    /// <summary>
    /// Verhaltenstest (Nachtrag): Eine auf dem Fremd-Thread eingereihte Entscheidung darf eine
    /// zwischenzeitliche, neuere Design-Wahl des Benutzers nicht ueberschreiben. Reproduziert genau
    /// den Fehlerfall der alten Fassung: Preferenz wird zwischen Einreihen und Ausfuehren von
    /// "System" auf eine feste Wahl geaendert - mit der alten Fassung waere trotzdem der vorher
    /// aufgeloeste Wert angewendet worden.
    /// </summary>
    [Fact]
    public void Eine_zwischenzeitliche_Design_Wahl_ueberschreibt_eine_eingereihte_veraltete_Entscheidung_nicht()
    {
        RunOnStaThread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var praeferenz = ThemeManager.System;
            var aktuellesTheme = ThemeManager.Dark;
            var angewendet = new List<string>();

            var service = new WindowsThemeFollowService(
                readPreference: () => praeferenz,
                applyResolvedTheme: angewendet.Add,
                dispatcher: dispatcher,
                readCurrentTheme: () => aktuellesTheme,
                readWindowsAppsUseLightTheme: () => 1); // Windows meldet Hell -> wuerde auf Hell aufloesen

            AufFremdemThreadFeuern(service, UserPreferenceCategory.General);

            // Zwischen dem Einreihen (oben, Fremd-Thread) und der Ausfuehrung (Pumpe unten) waehlt
            // der Benutzer selbst fest Dunkel - "System" gilt nicht mehr.
            praeferenz = ThemeManager.Dark;
            aktuellesTheme = ThemeManager.Dark;

            dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

            Assert.Empty(angewendet);
        });
    }

    /// <summary>
    /// Verhaltenstest (Nachtrag, Coalescing): Zwei kurz aufeinanderfolgende Meldungen auf dem
    /// Fremd-Thread, bevor die erste ausgefuehrt wurde, reihen nur EINEN Dispatch ein.
    /// </summary>
    [Fact]
    public void Zwei_rasch_aufeinanderfolgende_Fremd_Thread_Meldungen_reihen_nur_einen_Dispatch_ein()
    {
        RunOnStaThread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var praeferenzLesungen = 0;

            var service = new WindowsThemeFollowService(
                readPreference: () => { praeferenzLesungen++; return ThemeManager.System; },
                applyResolvedTheme: _ => { },
                dispatcher: dispatcher,
                readCurrentTheme: () => ThemeManager.Light,
                readWindowsAppsUseLightTheme: () => 1); // bereits Hell -> SollNeuAnwenden liefert false, aber liest die Praeferenz

            AufFremdemThreadFeuern(service, UserPreferenceCategory.General);
            AufFremdemThreadFeuern(service, UserPreferenceCategory.Color);

            dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

            Assert.Equal(1, praeferenzLesungen);
        });
    }

    private static void AufFremdemThreadFeuern(WindowsThemeFollowService service, UserPreferenceCategory kategorie)
    {
        var methode = typeof(WindowsThemeFollowService).GetMethod(
            "OnUserPreferenceChanged", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(methode);

        var thread = new Thread(() =>
            methode!.Invoke(service, [null, new UserPreferenceChangedEventArgs(kategorie)]));
        thread.Start();
        thread.Join();
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
