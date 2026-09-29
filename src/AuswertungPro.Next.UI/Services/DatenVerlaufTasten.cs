using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Kleine WPF-Regeln fuer Rueckgaengig/Wiederholen (Optik Aufgabe 16): wann Strg+Z dem Textfeld
/// gehoert, wie der Menuetext lautet und wie eine Meldung aus einem Hintergrund-Import auf den
/// UI-Thread kommt.
/// </summary>
public static class DatenVerlaufTasten
{
    /// <summary>
    /// Hat ein Texteditor den Fokus (Zelle im Bearbeitungsmodus, Formularfeld, editierbare Auswahl),
    /// gilt dessen eigenes Rueckgaengig — Strg+Z nimmt dann keine Datenaenderung zurueck.
    /// </summary>
    public static bool TexteingabeHatFokus()
        => System.Windows.Application.Current is not null && IstTexteingabe(Keyboard.FocusedElement);

    public static bool IstTexteingabe(object? element) => element is TextBoxBase or PasswordBox;

    /// <summary>«Rückgängig: Material 10001-10002»; ein Unterstrich im Namen bleibt sichtbar (kein Zugriffstaste-Zeichen).</summary>
    public static string MenuText(string aktion, string? beschreibung)
        => string.IsNullOrWhiteSpace(beschreibung)
            ? aktion
            : $"{aktion}: {beschreibung.Replace("_", "__", StringComparison.Ordinal)}";

    /// <summary>
    /// Nachtrag (Optikanalyse 28.09.2026): wie <see cref="MenuText"/>, aber OHNE die Verdopplung
    /// des Unterstrichs. Die Verdopplung ist nur innerhalb eines WPF-Menues noetig (dort wuerde ein
    /// einzelner Unterstrich sonst als Zugriffstaste-Zeichen verschluckt); ausserhalb eines Menues
    /// - etwa in der Trefferliste der globalen Suche (Strg+K) - wird der Text als reiner
    /// <c>TextBlock</c> angezeigt, der keine Zugriffstasten kennt. Dort waere ein verdoppelter
    /// Unterstrich ein sichtbarer Darstellungsfehler statt einer Schutzmassnahme.
    /// </summary>
    public static string SuchText(string aktion, string? beschreibung)
        => string.IsNullOrWhiteSpace(beschreibung)
            ? aktion
            : $"{aktion}: {beschreibung}";

    /// <summary>true, wenn die Aktion auf den UI-Thread verschoben wurde (und hier nichts mehr zu tun ist).</summary>
    public static bool AufUiThreadVerschoben(Action aktion)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess() || dispatcher.HasShutdownStarted)
            return false;
        dispatcher.BeginInvoke(aktion);
        return true;
    }
}
