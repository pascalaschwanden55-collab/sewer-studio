using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Prueft ob offene Streckenschaeden existieren (IsStreckenschaden=true, MeterEnd=null).
    /// Zeigt Dialog mit Liste und bietet an, sie am aktuellen Meter zu schliessen.
    /// Rueckgabe: true = weiter (geschlossen oder ignoriert), false = abgebrochen (User will weiter codieren).
    /// </summary>
    private bool CloseOpenStreckenschaeden(double currentMeter)
    {
        if (_codingVm == null) return true;

        var offene = CodingOpenStretchDamagePolicy.FindOpen(_codingVm.Events);

        if (offene.Count == 0) return true;

        var prompt = CodingOpenStretchDamagePromptBuilder.Build(offene, currentMeter);
        SuspendCodingOverlayInput();
        DialogConfirm result;
        try
        {
            result = DialogHost.Current.ConfirmCancel(
                prompt,
                "Offene Streckenschäden");
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (result == DialogConfirm.Yes)
        {
            // Alle offenen Streckenschaeden schliessen.
            // MeterEnd = letzte Sichtung (MeterAtCapture) oder aktueller Meter
            foreach (var ev in offene)
            {
                ev.Entry.MeterEnd = CodingOpenStretchDamagePolicy.ResolveCloseMeter(ev, currentMeter);
                _codingSessionService?.UpdateEvent(ev.EventId, ev.Entry, ev.Overlay);
            }
            RefreshCodingEventsList();
            return true;
        }

        if (result == DialogConfirm.Cancel)
            return false; // User will weiter codieren - Exit abbrechen

        return true; // "Nein" -> weiter ohne Schliessen
    }
}
