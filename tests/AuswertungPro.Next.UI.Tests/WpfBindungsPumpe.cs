using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// WPF wertet eine Bindung nach einem DataContext-Wechsel nicht sofort aus, sondern reiht die
/// Auswertung in die Dispatcher-Warteschlange (Prioritaet <c>DataBind</c>) ein. In einem
/// nackten STA-Test laeuft keine Nachrichtenschleife; ohne diesen Anstoss bliebe jede Zelle
/// auf ihrem Standardwert stehen und ein Test wuerde etwas Falsches belegen.
/// </summary>
internal static class WpfBindungsPumpe
{
    /// <summary>Arbeitet alles ab, was bis einschliesslich Leerlauf-Prioritaet ansteht.</summary>
    public static void Leeren()
        => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);
}
