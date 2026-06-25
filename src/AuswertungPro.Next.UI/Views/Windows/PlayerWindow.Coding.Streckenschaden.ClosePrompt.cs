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
        if (!_codingSessionHost.HasViewModel) return true;

        var offene = CodingOpenStretchDamagePolicy.FindOpen(_codingSessionHost.Events);

        if (offene.Count == 0) return true;

        var decision = RunWithSuspendedCodingOverlayInput(() =>
            CodingOpenStretchDamageDialogServiceFactory.Create()
                .ConfirmClose(offene, currentMeter));

        if (decision == CodingOpenStretchDamageDialogDecision.Close)
        {
            if (CodingOpenStretchDamageCloseApplier.Apply(offene, currentMeter, _codingSessionRuntimeOwner.Service))
                RefreshCodingEventsList();
            return true;
        }

        if (decision == CodingOpenStretchDamageDialogDecision.Cancel)
            return false;

        return true;
    }
}
