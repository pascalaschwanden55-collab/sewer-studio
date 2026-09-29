using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Schlusswelle (Item 6): <see cref="AuswertungPro.Next.UI.Services.WindowsThemeFollowService"/>
/// reagiert auf <c>SystemEvents.UserPreferenceChanged</c> - das feuert auf einem WinForms-
/// Botschaftsfenster-Thread ausserhalb von WPF, NICHT auf dem UI-Thread. Ein <c>Dispatcher.Invoke</c>
/// dort wuerde diesen Systemthread synchron blockieren, bis die UI-Warteschlange den Aufruf
/// abgearbeitet hat (bei einem beschaeftigten UI-Thread haelt das den Windows-Benachrichtigungsweg
/// unnoetig auf). <c>Dispatcher.BeginInvoke</c> reiht nur ein.
///
/// Ein deterministischer End-to-End-Test scheitert hier an zwei nicht mockbaren globalen
/// Abhaengigkeiten: der reale Windows-Registry-Wert (<c>WindowsThemeRegistry.ReadAppsUseLightTheme</c>,
/// direkt in der privaten Methode aufgerufen, nicht injizierbar) und der statische
/// <c>ThemeManager.CurrentTheme</c>. Die reine Entscheidungsregel selbst ist bereits vollstaendig
/// deterministisch getestet (<c>WindowsThemeFollowPolicyTests</c>, falls vorhanden) — dieser
/// Waechter sichert stattdessen strukturell die Dispatch-Stelle selbst ab: kein blockierendes
/// <c>Invoke</c> mehr auf dem Nicht-UI-Zweig, das Skip-wenn-unveraendert bleibt VOR jedem Dispatch.
/// </summary>
public sealed class WindowsThemeFollowServiceDispatchTests
{
    private static string Quelle()
        => File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Services", "WindowsThemeFollowService.cs"));

    [Fact]
    public void Der_nicht_ui_thread_zweig_verwendet_BeginInvoke_statt_Invoke()
    {
        var quelle = Quelle();
        var methodenStart = quelle.IndexOf("private void OnUserPreferenceChanged", StringComparison.Ordinal);
        Assert.True(methodenStart >= 0, "OnUserPreferenceChanged wurde nicht gefunden.");
        var methodenEnde = quelle.IndexOf("\n    public void Dispose()", methodenStart, StringComparison.Ordinal);
        Assert.True(methodenEnde > methodenStart, "Das Ende von OnUserPreferenceChanged wurde nicht gefunden.");
        var methode = quelle[methodenStart..methodenEnde];

        Assert.Contains("_dispatcher.BeginInvoke(", methode);
        Assert.DoesNotContain("_dispatcher.Invoke(", methode);

        // Struktur bleibt: erst der fruehe Ausstieg (SollNeuAnwenden), dann CheckAccess/BeginInvoke -
        // das Skip-wenn-unveraendert wird nie erst NACH einem Dispatch geprueft.
        var indexSkip = methode.IndexOf("WindowsThemeFollowPolicy.SollNeuAnwenden(", StringComparison.Ordinal);
        var indexDispatch = methode.IndexOf("_dispatcher.CheckAccess()", StringComparison.Ordinal);
        Assert.True(indexSkip >= 0 && indexDispatch > indexSkip,
            "Die Skip-wenn-unveraendert-Pruefung muss vor dem Dispatch stehen.");
    }

    /// <summary>Der UI-Thread-Zweig (bereits auf dem richtigen Thread) bleibt ein DIREKTER,
    /// synchroner Aufruf ohne Umweg ueber den Dispatcher — nur der Fremd-Thread-Fall wechselt auf
    /// BeginInvoke.</summary>
    [Fact]
    public void Der_ui_thread_zweig_ruft_weiterhin_direkt_auf()
    {
        var methode = Quelle();
        Assert.Contains("if (_dispatcher.CheckAccess())\n            _applyResolvedTheme(resolved);", methode);
    }
}
