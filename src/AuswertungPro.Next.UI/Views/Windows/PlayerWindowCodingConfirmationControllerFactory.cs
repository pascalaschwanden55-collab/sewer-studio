using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

internal sealed record PlayerWindowCodingConfirmationControllerDependencies(
    CodingPendingConfirmationStateController PendingState,
    CodingSessionServiceOwner SessionRuntimeOwner,
    ICodingSessionHost SessionHost,
    CodingTrainingPersistenceContext TrainingPersistence,
    Action RefreshEvents,
    CodingConfirmationPanelControlsOwner ConfirmationPanel,
    CodingEventsListControls EventsList,
    TextBlock CurrentStatusText,
    ToggleButton LiveAiToggle,
    CodingAiControllerOwner AiRuntimeOwner,
    PlayerPlaybackControlHost PlaybackControlHost,
    ILiveDetectionStatusController StatusController);

internal static class PlayerWindowCodingConfirmationControllerFactory
{
    internal static ICodingConfirmationController Create(
        PlayerWindowCodingConfirmationControllerDependencies dependencies)
    {
        Validate(dependencies);

        var decision = new CodingConfirmationDecisionController(
            dependencies.PendingState,
            new CodingConfirmationDecisionControllerActions(
                ResolveCodingSessionService: () => dependencies.SessionRuntimeOwner.Service,
                ResolveCodingEvents: () => dependencies.SessionHost.EventCollection,
                PersistTrainingSample: (codingEvent, _) =>
                    dependencies.TrainingPersistence.PersistSingleEventAsync(codingEvent),
                RefreshCodingEvents: dependencies.RefreshEvents,
                HideConfirmationPanel: dependencies.ConfirmationPanel.Hide,
                ShowPersistenceError: error =>
                {
                    dependencies.StatusController.SetCodingAiState(
                        "Goldspeichern fehlgeschlagen",
                        PlayerStatusColors.Error,
                        error);
                    dependencies.ConfirmationPanel.ShowPersistenceError(error);
                },
                SelectEvent: dependencies.EventsList.SelectEvent,
                IsLiveAiEnabled: () => PlayerToggleButtonControls.IsChecked(dependencies.LiveAiToggle),
                ResolveModelName: () => dependencies.AiRuntimeOwner.Controller.ModelName,
                SetPause: dependencies.PlaybackControlHost.SetPause,
                ApplyResumeStatus: status => dependencies.StatusController.SetCodingAiState(
                    status.StatusText,
                    PlayerStatusColors.Success,
                    status.DetailText)));

        return new CodingConfirmationController(
            dependencies.PendingState,
            new CodingConfirmationControllerBindings(
                ResolveCurrentStatusText: () => dependencies.CurrentStatusText.Text,
                ResolveCodingSessionService: () => dependencies.SessionRuntimeOwner.Service,
                SetPause: dependencies.PlaybackControlHost.SetPause,
                ApplyConfirmationPanel: dependencies.ConfirmationPanel.Apply,
                ShowStatus: (status, color, detail) =>
                    dependencies.StatusController.SetCodingAiState(status, color, detail),
                Accept: decision.Accept,
                Edit: decision.Edit,
                Reject: decision.Reject,
                RetrySave: decision.RetrySave));
    }

    private static void Validate(PlayerWindowCodingConfirmationControllerDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(dependencies.PendingState);
        ArgumentNullException.ThrowIfNull(dependencies.SessionRuntimeOwner);
        ArgumentNullException.ThrowIfNull(dependencies.SessionHost);
        ArgumentNullException.ThrowIfNull(dependencies.TrainingPersistence);
        ArgumentNullException.ThrowIfNull(dependencies.RefreshEvents);
        ArgumentNullException.ThrowIfNull(dependencies.ConfirmationPanel);
        ArgumentNullException.ThrowIfNull(dependencies.EventsList);
        ArgumentNullException.ThrowIfNull(dependencies.CurrentStatusText);
        ArgumentNullException.ThrowIfNull(dependencies.LiveAiToggle);
        ArgumentNullException.ThrowIfNull(dependencies.AiRuntimeOwner);
        ArgumentNullException.ThrowIfNull(dependencies.PlaybackControlHost);
        ArgumentNullException.ThrowIfNull(dependencies.StatusController);
    }
}
