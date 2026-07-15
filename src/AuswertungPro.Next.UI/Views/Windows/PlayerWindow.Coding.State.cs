using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private readonly CodingRuntimeStateControllerSet _codingRuntimeStates = new();
    private readonly CodingSchemaStateControllerSet _codingSchemaStates = new();

    private readonly CodingOverlayStateControllerSet _codingOverlayStates = new();

    private readonly CodingAiStateControllerSet _codingAiStates = new();
    private readonly CodingStreckenschadenTrackerOwner _streckenschadenTracker = new();
    private readonly CodingOsdMeterController _codingOsdMeterController = new();
    private readonly CodingPhotoCaptureServicesOwner _codingPhotoCaptureServicesOwner = new();
    private readonly Ai.CodingTrainingPersistenceContext _codingTrainingPersistenceContext;
    private readonly CodingSidePanelControllerSet _codingSidePanelControllers = new();
    private readonly CodingConfirmationPanelControlsOwner _codingConfirmationPanelControls = new();
    private readonly ICodingConfirmationController _codingConfirmationController;
    private readonly ICodingInlineDefectController _codingInlineDefectController;
    private readonly CodingSessionViewModelOwner _codingSessionViewModelOwner;
    private readonly ICodingSessionHost _codingSessionHost;
    private readonly ICodingOverlayToolHost _codingOverlayToolHost;
    private readonly Ai.CodingFindingContext _codingFindingContext;
    private readonly Ai.CodingAnalysisContext _codingAnalysisContext;
    private readonly Ai.CodingBoundaryContext _codingBoundaryContext;
    private readonly ICodingPipelineHealthController _codingPipelineHealthController;
    private readonly CodingSchemaOverlayController _codingSchemaOverlayController;
    private readonly CodingManualCalibrationController _codingManualCalibrationController;
    private readonly CodingCalibrationPointerController _codingCalibrationPointerController;
    private readonly CodingEventsRefreshController _codingEventsRefreshController;
    private readonly ICodingProtocolMatchController _codingProtocolMatchController;
    private readonly ICodingEingabemarkerInteractionController _codingEingabemarkerInteractionController;
    private readonly ICodingEingabemarkerSubmissionController _codingEingabemarkerSubmissionController;
    private readonly ICodingEingabemarkerInputController _codingEingabemarkerInputController;
    private readonly CodingProtocolStateControllerSet _codingProtocolStates = new();
    private readonly CodingNavigationController _codingNavigationController;

    private CodingModeStateController _codingModeState => _codingRuntimeStates.ModeState;

    private CodingSessionServiceOwner _codingSessionRuntimeOwner => _codingRuntimeStates.SessionRuntimeOwner;

    private CodingOverlayServiceOwner _codingOverlayRuntimeOwner => _codingRuntimeStates.OverlayRuntimeOwner;

    private CodingSchemaOverlayManagerOwner _codingSchemaManager => _codingSchemaStates.OverlayManagerOwner;

    private CodingSchemaTypeStateController _codingSchemaTypeState => _codingSchemaStates.TypeState;

    private CodingCalibrationStateController _codingCalibrationState => _codingOverlayStates.CalibrationState;

    private CodingOverlayInputVisibilityStateController _codingOverlayInputVisibilityState => _codingOverlayStates.InputVisibilityState;

    private CodingOverlayRenderStateController _codingOverlayRenderState => _codingOverlayStates.RenderState;

    private CodingActiveToolNameStateController _codingActiveToolNameState => _codingOverlayStates.ActiveToolNameState;

    private LiveDetectionPulseStateController _codingAiPulseStateController => _codingAiStates.PulseState;

    private CodingAiOverlayAutoHideTimerOwner _codingAiOverlayAutoHideTimerOwner => _codingAiStates.OverlayAutoHideTimerOwner;

    private CodingAiControllerOwner _codingAiRuntimeOwner => _codingAiStates.RuntimeOwner;

    private CodingFrameReadinessController _codingFrameReadinessController => _codingAiStates.FrameReadinessController;

    private CodingLiveAiTimerControllerOwner _codingLiveAiTimerOwner => _codingAiStates.LiveTimerOwner;

    private CodingImportReferenceEventsOwner _codingImportReferenceEvents => _codingProtocolStates.ImportReferenceEvents;

    private CodingNavigationPendingState _codingNavigationPendingState => _codingProtocolStates.NavigationPendingState;

    private CodingProtocolMatchStateController _codingProtocolMatchState => _codingProtocolStates.ProtocolMatchState;

    private CodingPendingConfirmationStateController _codingPendingConfirmationState => _codingProtocolStates.PendingConfirmationState;

    private CodingBaselineSignatureStateController _codingBaselineSignatureState => _codingProtocolStates.BaselineSignatureState;
}
