using System.Linq;
using System.Text;
using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

// Slice 8a Auto-BCD/BCE/Streckenschaden Step 2 — Pre-Complete-Hook fuer
// offene Streckenschaeden. Mini-ADR:
// docs/adrs/2026-05-10-slice-8a-auto-bcd-bce-strecke.md
//
// Q3=B: Legacy-YesNoCancel-Dialog statt Exception.
// Q4=A: Dialog-Logik im Window Code-Behind (eigene Partial).
// Q5=A: Service-Overload `CompleteSession(bool allowOpen)` aus Step 1.
//
// Step 3 verdrahtet diesen Hook im "Codierung abschliessen"-Pfad.
public partial class CodingModeWindow
{
    /// <summary>Pre-Complete-Hook: prueft auf offene Streckenschaeden und
    /// laesst den User entscheiden, was passieren soll.
    ///
    /// Rueckgabe + out-Parameter:
    /// - Returnt false → User hat "Cancel" geklickt, der Abschluss soll
    ///   abgebrochen werden. Caller sollte den CompleteSession-Call
    ///   ueberspringen.
    /// - Returnt true mit allowOpen=false → keine offenen Streckenschaeden,
    ///   ODER User hat "Yes" geklickt; alle offenen wurden bei aktuellem
    ///   Meter geschlossen. Caller darf CompleteSession() default rufen.
    /// - Returnt true mit allowOpen=true → User hat "No" geklickt; offene
    ///   Streckenschaeden bleiben im Protokoll (mit MeterEnd=null). Caller
    ///   muss CompleteSession(allowOpenStreckenschaden: true) rufen.</summary>
    private bool ConfirmOpenStreckenschadenAndChooseAction(out bool allowOpen)
    {
        allowOpen = false;

        var offene = _sessionService.GetOpenStreckenschaeden();
        if (offene.Count == 0)
            return true; // Sauberer Abschluss, kein Dialog.

        var sb = new StringBuilder();
        sb.AppendLine("Folgende Streckenschaeden sind noch offen (kein MeterEnde):");
        sb.AppendLine();
        foreach (var ev in offene)
        {
            sb.AppendLine($"  • {ev.Entry.Code} – {ev.Entry.Beschreibung}");
            sb.AppendLine($"    Start: {ev.MeterAtCapture:F2}m");
        }

        var currentMeter = _sessionService.CurrentMeter;
        sb.AppendLine();
        sb.AppendLine($"Sollen alle offenen Streckenschaeden bei {currentMeter:F2}m geschlossen werden?");
        sb.AppendLine();
        sb.AppendLine("Ja: alle bei aktuellem Meter schliessen, dann Codierung abschliessen.");
        sb.AppendLine("Nein: ohne Schliessen abschliessen (im Protokoll als offen markiert).");
        sb.AppendLine("Abbrechen: zurueck zur Codierung.");

        var result = _dialogs.ShowMessage(
            sb.ToString(),
            "Offene Streckenschaeden",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        switch (result)
        {
            case MessageBoxResult.Yes:
                // Alle offenen Streckenschaeden bei MeterAtCapture (oder aktuellem
                // Meter wenn Kamera noch zurueck steht) schliessen. Service wirft
                // wenn endMeter <= MeterStart — wir nutzen MAX davon.
                foreach (var ev in offene)
                {
                    var start = ev.Entry.MeterStart ?? 0;
                    var endMeter = ev.MeterAtCapture > start
                        ? ev.MeterAtCapture
                        : currentMeter;
                    if (endMeter <= start)
                        endMeter = start + 0.01; // mindestens 1cm
                    _sessionService.CloseStreckenschaden(ev.EventId, endMeter);
                }
                return true;

            case MessageBoxResult.No:
                allowOpen = true;
                return true;

            case MessageBoxResult.Cancel:
            default:
                return false;
        }
    }
}
