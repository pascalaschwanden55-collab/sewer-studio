using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Prueft ob offene Streckenschaeden existieren (IsStreckenschaden=true, MeterEnd=null).
    /// Bietet an, sie am aktuellen Meter zu schliessen.
    /// Rueckgabe: true = weiter (geschlossen oder ignoriert), false = abgebrochen.
    /// </summary>
    private bool CloseOpenStreckenschaeden(double currentMeter)
    {
        if (_codingVm == null) return true;

        var offene = CodingOpenStretchDamagePolicy.FindOpen(_codingVm.Events);

        if (offene.Count == 0) return true;

        SuspendCodingOverlayInput();
        CodingOpenStretchDamageDialogDecision decision;
        try
        {
            decision = CodingOpenStretchDamageDialogServiceFactory.Create()
                .ConfirmClose(offene, currentMeter);
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (decision == CodingOpenStretchDamageDialogDecision.Close)
        {
            foreach (var ev in offene)
            {
                ev.Entry.MeterEnd = CodingOpenStretchDamagePolicy.ResolveCloseMeter(ev, currentMeter);
                _codingSessionService?.UpdateEvent(ev.EventId, ev.Entry, ev.Overlay);
            }
            RefreshCodingEventsList();
            return true;
        }

        if (decision == CodingOpenStretchDamageDialogDecision.Cancel)
            return false;

        return true;
    }
}
