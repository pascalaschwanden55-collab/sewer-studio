using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private readonly CodingModeStateController _codingModeState = new();
    private readonly CodingSessionServiceOwner _codingSessionRuntimeOwner = new();
    private readonly CodingOverlayServiceOwner _codingOverlayRuntimeOwner = new();
    private readonly CodingSchemaOverlayManagerOwner _codingSchemaManager = new();
    private readonly CodingSchemaTypeStateController _codingSchemaTypeState = new();

    private readonly CodingCalibrationStateController _codingCalibrationState = new();
    private readonly CodingOverlayInputVisibilityStateController _codingOverlayInputVisibilityState = new();
    private readonly CodingOverlayRenderStateController _codingOverlayRenderState = new();
    private readonly CodingActiveToolNameStateController _codingActiveToolNameState = new();

    private readonly LiveDetectionPulseStateController _codingAiPulseStateController = new();
    private readonly CodingAiOverlayAutoHideTimerOwner _codingAiOverlayAutoHideTimerOwner = new();
    private readonly CodingStreckenschadenTrackerOwner _streckenschadenTracker = new();
    private readonly CodingAiControllerOwner _codingAiRuntimeOwner = new();
    private readonly CodingFrameReadinessController _codingFrameReadinessController = new();
    private readonly CodingLiveAiTimerControllerOwner _codingLiveAiTimerOwner = new();
    private readonly CodingOsdMeterController _codingOsdMeterController = new();
    private readonly CodingPhotoCaptureServicesOwner _codingPhotoCaptureServicesOwner = new();
    private readonly CodingSidePanelControllerSet _codingSidePanelControllers = new();
    private readonly CodingConfirmationPanelControlsOwner _codingConfirmationPanelControls = new();
    private readonly CodingSessionViewModelOwner _codingSessionViewModelOwner;
    private readonly ICodingSessionHost _codingSessionHost;
    private readonly ICodingOverlayToolHost _codingOverlayToolHost;
    private readonly CodingNavigationPendingState _codingNavigationPendingState = new();

    private readonly CodingEingabemarkerStateController _eingabemarkerState = new();

    private readonly CodingImportReferenceEventsOwner _codingImportReferenceEvents = new();
    private readonly CodingProtocolMatchStateController _codingProtocolMatchState = new();

    private readonly CodingPendingConfirmationStateController _codingPendingConfirmationState = new();

    private readonly CodingBaselineSignatureStateController _codingBaselineSignatureState = new();
}
