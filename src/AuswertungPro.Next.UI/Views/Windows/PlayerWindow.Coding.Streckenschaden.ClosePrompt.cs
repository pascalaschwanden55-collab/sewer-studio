using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

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
        var result = CodingOpenStretchDamagePromptCommandWorkflow.Execute(
            new CodingOpenStretchDamagePromptCommandRequest(
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                Events: _codingSessionHost.Events,
                CurrentMeter: currentMeter),
            new CodingOpenStretchDamagePromptCommandActions(
                FindOpen: CodingOpenStretchDamagePolicy.FindOpen,
                ConfirmClose: (openEvents, closeMeter) => CodingOpenStretchDamageDialogWorkflow.ConfirmClose(
                    openEvents,
                    closeMeter,
                    runWithSuspendedOverlay: callback => _codingOverlayInputVisibilityController.Run(callback)),
                ApplyClose: (openEvents, closeMeter) => CodingOpenStretchDamageCloseApplier.Apply(
                    openEvents,
                    closeMeter,
                    _codingSessionRuntimeOwner.Service),
                RefreshEvents: RefreshCodingEventsList));

        return result.ShouldContinue;
    }
}
