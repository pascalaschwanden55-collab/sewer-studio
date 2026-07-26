using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

internal sealed record PlayerWindowCodingEingabemarkerControls(
    Popup CodingOverlayPopup,
    Canvas CodingOverlayCanvas,
    ToggleButton Toggle,
    FrameworkElement InputPopup,
    TextBox InputText,
    ComboBox QuickSelection);

internal sealed record PlayerWindowCodingEingabemarkerActions(
    Action UpdateCodingOverlayViewport,
    Func<ProtocolEntry, string?> CapturePhoto,
    Action RefreshEvents,
    Action UpdateToolBadge,
    Func<string, Task> RunAiFallbackAsync,
    Action ClearDetectionOverlays);

internal sealed record PlayerWindowCodingEingabemarkerControllerSetDependencies(
    PlayerWindowCodingEingabemarkerControls Controls,
    PlayerPlaybackControlHost PlaybackControlHost,
    ILiveDetectionMarkToolController MarkToolController,
    ILiveDetectionStatusController StatusController,
    ICodingSessionHost SessionHost,
    CodingSessionServiceOwner SessionServiceOwner,
    CodingOsdMeterController OsdMeterController,
    PlayerTimelineHost TimelineHost,
    CodingFindingContext FindingContext,
    CodingTrainingPersistenceContext TrainingPersistence,
    Dispatcher Dispatcher,
    PlayerWindowCodingEingabemarkerActions Actions);

internal sealed record PlayerWindowCodingEingabemarkerControllerSetFactoryActions(
    Action<Task, string> ObserveTask);

internal sealed record PlayerWindowCodingEingabemarkerControllerSet(
    ICodingEingabemarkerInteractionController Interaction,
    ICodingEingabemarkerSubmissionController Submission,
    ICodingEingabemarkerInputController Input);

internal static class PlayerWindowCodingEingabemarkerControllerSetFactory
{
    internal static PlayerWindowCodingEingabemarkerControllerSet Create(
        PlayerWindowCodingEingabemarkerControllerSetDependencies dependencies)
        => Create(
            dependencies,
            new PlayerWindowCodingEingabemarkerControllerSetFactoryActions(
                ObserveTask: (task, operation) =>
                    task.SafeFireAndForget(operation)));

    internal static PlayerWindowCodingEingabemarkerControllerSet Create(
        PlayerWindowCodingEingabemarkerControllerSetDependencies dependencies,
        PlayerWindowCodingEingabemarkerControllerSetFactoryActions factoryActions)
    {
        Validate(dependencies, factoryActions);

        var interaction = CreateInteraction(dependencies);
        var submission = CreateSubmission(dependencies, factoryActions, interaction);
        var input = CreateInput(dependencies, factoryActions, interaction, submission);

        return new PlayerWindowCodingEingabemarkerControllerSet(
            interaction,
            submission,
            input);
    }

    private static ICodingEingabemarkerInteractionController CreateInteraction(
        PlayerWindowCodingEingabemarkerControllerSetDependencies dependencies)
    {
        var controls = dependencies.Controls;

        return new CodingEingabemarkerInteractionController(
            new CodingEingabemarkerInteractionControllerBindings(
                PauseForCodingInteraction: () => PlayerCodingPlayback.PauseForCodingInteraction(
                    dependencies.PlaybackControlHost.SetPause),
                EnsureMarkOverlayReady: dependencies.MarkToolController.EnsureOverlayReady,
                OpenCodingOverlayPopup: () => CodingOverlayInputControls.OpenPopup(
                    controls.CodingOverlayPopup),
                UpdateCodingOverlayViewport: dependencies.Actions.UpdateCodingOverlayViewport,
                EnableDrawingCanvas: () => CodingOverlayInputControls.EnableDrawingCanvas(
                    controls.CodingOverlayCanvas),
                ShowDrawingStatus: () => dependencies.StatusController.SetCodingAiState(
                    "Eingabemarker: Rechteck um die Beobachtung ziehen",
                    PlayerStatusColors.Info,
                    "Klicken + Ziehen = Bereich markieren"),
                UncheckButton: () => PlayerToggleButtonControls.Uncheck(controls.Toggle),
                HideInputPopup: () => CodingEingabemarkerPopupControls.Hide(controls.InputPopup),
                ClearPreview: preview => CodingEingabemarkerPreviewRenderer.Clear(
                    controls.CodingOverlayCanvas,
                    preview),
                ResetCanvasCursor: () => CodingOverlayInputControls.ResetCanvasCursor(
                    controls.CodingOverlayCanvas),
                CaptureMouse: () => CodingOverlayInputControls.CaptureCanvasMouse(
                    controls.CodingOverlayCanvas),
                CreatePreview: point => CodingEingabemarkerPreviewRenderer.Create(
                    controls.CodingOverlayCanvas,
                    point),
                UpdatePreview: CodingEingabemarkerPreviewRenderer.Update,
                ReleaseMouseCapture: () => CodingOverlayInputControls.ReleaseCanvasMouse(
                    controls.CodingOverlayCanvas),
                ResolveCanvasSize: () => CodingOverlayInputControls.GetCanvasActualSize(
                    controls.CodingOverlayCanvas),
                DisableDrawingCanvas: () => CodingOverlayInputControls.DisableDrawingCanvas(
                    controls.CodingOverlayCanvas),
                ShowInputPopup: () => CodingEingabemarkerPopupControls.ShowInput(
                    controls.InputPopup,
                    controls.InputText,
                    controls.QuickSelection),
                FocusInput: () => PlayerDispatcherScheduler.ScheduleInput(
                    dependencies.Dispatcher,
                    () => PlayerFocusControls.FocusElement(controls.InputText)),
                ShowInputStatus: () => dependencies.StatusController.SetCodingAiState(
                    "Beschreibung eingeben oder Stichwort wÃ¤hlen, dann Enter",
                    PlayerStatusColors.Info,
                    "z.B. \"Beule unten\", \"Riss bei 3 Uhr\", \"Anschluss offen\"")));
    }

    private static ICodingEingabemarkerSubmissionController CreateSubmission(
        PlayerWindowCodingEingabemarkerControllerSetDependencies dependencies,
        PlayerWindowCodingEingabemarkerControllerSetFactoryActions factoryActions,
        ICodingEingabemarkerInteractionController interaction)
        => new CodingEingabemarkerSubmissionController(
            new CodingEingabemarkerSubmissionControllerBindings(
                HasCodingViewModel: () => dependencies.SessionHost.HasViewModel,
                ResolveCodingSessionService: () => dependencies.SessionServiceOwner.Service,
                HideInput: () => CodingEingabemarkerPopupControls.Hide(
                    dependencies.Controls.InputPopup),
                SetAnalyzingPhase: interaction.SetAnalyzingPhase,
                ResolveCodeHint: PlayerVsaCodeHintResolver.ResolveKeyword,
                ResolveEvents: () => dependencies.SessionHost.Events,
                ShowDuplicateStatus: (codeHint, meter) =>
                    dependencies.StatusController.SetCodingAiState(
                        $"{codeHint} bereits vorhanden bei {meter:F2}m - Duplikat",
                        PlayerStatusColors.Warning,
                        ""),
                ResolveCurrentOverlay: () => dependencies.SessionHost.CurrentOverlay,
                ResolveMeter: () =>
                    dependencies.OsdMeterController.LastMeter
                    ?? dependencies.SessionHost.CurrentMeter,
                ResolveVideoTime: () =>
                    dependencies.SessionHost.CurrentVideoTime
                    ?? dependencies.TimelineHost.CurrentTimeOrZero,
                LookupLabel: dependencies.FindingContext.LookupLabel,
                CapturePhoto: dependencies.Actions.CapturePhoto,
                RefreshEvents: dependencies.Actions.RefreshEvents,
                UpdateToolBadge: dependencies.Actions.UpdateToolBadge,
                PersistTraining: codingEvent => factoryActions.ObserveTask(
                    dependencies.TrainingPersistence.PersistSingleEventAsync(codingEvent),
                    "TrainingSaveSingle"),
                ShowSuccessStatus: (code, label, meter) =>
                    dependencies.StatusController.SetCodingAiState(
                        $"{code} {label} bei {meter:F2}m eingetragen",
                        PlayerStatusColors.Success,
                        ""),
                ShowAiFallbackStatus: keyword =>
                    dependencies.StatusController.SetCodingAiState(
                        $"KI analysiert: \"{keyword}\" ...",
                        PlayerStatusColors.Warning,
                        "Qwen analysiert"),
                RunAiFallbackAsync: dependencies.Actions.RunAiFallbackAsync,
                ShowErrorStatus: message => dependencies.StatusController.SetCodingAiState(
                    $"Fehler: {message}",
                    PlayerStatusColors.Error,
                    ""),
                CancelMarker: () => interaction.Cancel(),
                PersistTrainingAsync:
                    dependencies.TrainingPersistence.PersistSingleEventAsync));

    private static ICodingEingabemarkerInputController CreateInput(
        PlayerWindowCodingEingabemarkerControllerSetDependencies dependencies,
        PlayerWindowCodingEingabemarkerControllerSetFactoryActions factoryActions,
        ICodingEingabemarkerInteractionController interaction,
        ICodingEingabemarkerSubmissionController submission)
        => new CodingEingabemarkerInputController(
            new CodingEingabemarkerInputControllerBindings(
                CancelMarker: () => interaction.Cancel(),
                ClearDetectionOverlays: dependencies.Actions.ClearDetectionOverlays,
                Submit: () => factoryActions.ObserveTask(
                    submission.SubmitAsync(dependencies.Controls.InputText.Text),
                    "SubmitEingabemarker"),
                ApplyQuickSelection: text => CodingEingabemarkerPopupControls.ApplyQuickSelection(
                    dependencies.Controls.InputText,
                    text)));

    private static void Validate(
        PlayerWindowCodingEingabemarkerControllerSetDependencies dependencies,
        PlayerWindowCodingEingabemarkerControllerSetFactoryActions factoryActions)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(dependencies.Controls);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.CodingOverlayPopup);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.CodingOverlayCanvas);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.Toggle);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.InputPopup);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.InputText);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.QuickSelection);
        ArgumentNullException.ThrowIfNull(dependencies.PlaybackControlHost);
        ArgumentNullException.ThrowIfNull(dependencies.MarkToolController);
        ArgumentNullException.ThrowIfNull(dependencies.StatusController);
        ArgumentNullException.ThrowIfNull(dependencies.SessionHost);
        ArgumentNullException.ThrowIfNull(dependencies.SessionServiceOwner);
        ArgumentNullException.ThrowIfNull(dependencies.OsdMeterController);
        ArgumentNullException.ThrowIfNull(dependencies.TimelineHost);
        ArgumentNullException.ThrowIfNull(dependencies.FindingContext);
        ArgumentNullException.ThrowIfNull(dependencies.TrainingPersistence);
        ArgumentNullException.ThrowIfNull(dependencies.Dispatcher);
        ArgumentNullException.ThrowIfNull(dependencies.Actions);
        ArgumentNullException.ThrowIfNull(dependencies.Actions.UpdateCodingOverlayViewport);
        ArgumentNullException.ThrowIfNull(dependencies.Actions.CapturePhoto);
        ArgumentNullException.ThrowIfNull(dependencies.Actions.RefreshEvents);
        ArgumentNullException.ThrowIfNull(dependencies.Actions.UpdateToolBadge);
        ArgumentNullException.ThrowIfNull(dependencies.Actions.RunAiFallbackAsync);
        ArgumentNullException.ThrowIfNull(dependencies.Actions.ClearDetectionOverlays);
        ArgumentNullException.ThrowIfNull(factoryActions);
        ArgumentNullException.ThrowIfNull(factoryActions.ObserveTask);
    }
}
