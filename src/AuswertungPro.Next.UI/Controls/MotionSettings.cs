using System.Windows;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Zentraler Schalter fuer Dauer-Animationen (Puls, Schimmer, Schweben, Neural-Kugel).
/// Kurze Ereignis-Animationen (Hover, Fokus, Ein-/Ausblenden) laufen immer — sie sind Rueckmeldung,
/// kein Schmuck, und werden hier bewusst nicht abgeschaltet.
///
/// Vorrang: ausdrueckliche Einstellung des Nutzers &gt; Windows-Systemeinstellung. Ohne gesetzte
/// Einstellung gilt, was Windows meldet (SystemParameters.ClientAreaAnimation ist dort false, wenn
/// der Nutzer Animationen systemweit abgeschaltet hat).
///
/// Muster wie Controls/AnimationTokens.cs: statisch, damit Controls im Code-Behind ohne
/// Konstruktor-Durchreichung darauf zugreifen koennen.
/// </summary>
public static class MotionSettings
{
    private static bool? _reduceMotionOverride;

    /// <summary>True = keine Endlos-Animationen starten; ruhende Endzustaende bleiben sichtbar.</summary>
    public static bool ReduceMotion
    {
        get => _reduceMotionOverride ?? !SystemParameters.ClientAreaAnimation;
        set => _reduceMotionOverride = value;
    }

    /// <summary>
    /// Uebernimmt die gespeicherte Einstellung beim Programmstart.
    /// Aufrufer: App.OnStartup, direkt nach dem Laden der Einstellungen.
    ///
    /// Der Schalter kann nur zusaetzlich reduzieren: Angehakt heisst "immer ruhig", nicht angehakt
    /// heisst "wie Windows es vorgibt". Sonst wuerde der Standardwert false den Systemwunsch eines
    /// Nutzers uebersteuern, der Animationen systemweit abgeschaltet hat.
    /// </summary>
    public static void Configure(bool reduceMotion) => _reduceMotionOverride = reduceMotion ? true : null;

    /// <summary>Nur fuer Tests: ausdrueckliche Einstellung verwerfen, wieder dem System folgen.</summary>
    public static void ResetForTests() => _reduceMotionOverride = null;
}
