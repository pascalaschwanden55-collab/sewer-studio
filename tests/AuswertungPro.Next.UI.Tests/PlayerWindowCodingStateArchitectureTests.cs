using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingStateArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_state_fields_live_in_coding_state_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var codingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var codingModeStatePath = Path.Combine(uiRoot, "Player", "CodingModeStateController.cs");
        var codingRuntimeStateControllerSetPath = Path.Combine(uiRoot, "Player", "CodingRuntimeStateControllerSet.cs");
        var codingSchemaStateControllerSetPath = Path.Combine(uiRoot, "Player", "CodingSchemaStateControllerSet.cs");
        var codingAiStateControllerSetPath = Path.Combine(uiRoot, "Player", "CodingAiStateControllerSet.cs");
        var importEventsOwnerPath = Path.Combine(uiRoot, "Player", "CodingImportReferenceEventsOwner.cs");
        var protocolStateControllerSetPath = Path.Combine(uiRoot, "Player", "CodingProtocolStateControllerSet.cs");
        var overlayStateControllerSetPath = Path.Combine(uiRoot, "Player", "CodingOverlayStateControllerSet.cs");
        var sidePanelControllerSetPath = Path.Combine(uiRoot, "Player", "CodingSidePanelControllerSet.cs");
        var sidePanelEventBinderPath = Path.Combine(windowsRoot, "PlayerCodingSidePanelEventBinder.cs");
        var sidePanelControllerInitializerPath = Path.Combine(windowsRoot, "PlayerCodingSidePanelControllerInitializer.cs");

        Assert.True(File.Exists(statePath), "Coding-Feldzustand soll aus dem allgemeinen Coding-Partial heraus.");
        Assert.True(File.Exists(codingModeStatePath), "Coding-Modus-Zustand soll nicht mehr als Rohfeld im PlayerWindow liegen.");
        Assert.True(File.Exists(codingRuntimeStateControllerSetPath), "Coding-Runtime-Zustand soll nicht einzeln im PlayerWindow liegen.");
        Assert.True(File.Exists(codingSchemaStateControllerSetPath), "Coding-Schema-Zustand soll nicht einzeln im PlayerWindow liegen.");
        Assert.True(File.Exists(codingAiStateControllerSetPath), "Coding-AI-Zustandscontroller sollen nicht einzeln im PlayerWindow liegen.");
        Assert.True(File.Exists(importEventsOwnerPath), "Coding-Import-Referenz-Events sollen nicht mehr als rohe Collection im PlayerWindow liegen.");
        Assert.True(File.Exists(protocolStateControllerSetPath), "Coding-Protocol/Navigations-Zustand soll nicht einzeln im PlayerWindow liegen.");
        Assert.True(File.Exists(overlayStateControllerSetPath), "Coding-Overlay-Zustandscontroller sollen nicht einzeln im PlayerWindow liegen.");
        Assert.True(File.Exists(sidePanelControllerSetPath), "Coding-SidePanel-Control-Wrapper sollen nicht mehr als einzelne Rohfelder im PlayerWindow liegen.");
        Assert.True(File.Exists(sidePanelEventBinderPath), "Coding-SidePanel-Event-Wiring soll ausserhalb des PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(sidePanelControllerInitializerPath), "Coding-SidePanel-Control-Mapping soll ausserhalb des PlayerWindow-Partials liegen.");

        var coding = File.ReadAllText(codingPath);
        var state = File.ReadAllText(statePath);
        var codingModeState = File.Exists(codingModeStatePath) ? File.ReadAllText(codingModeStatePath) : "";
        var codingRuntimeStateControllerSet = File.Exists(codingRuntimeStateControllerSetPath) ? File.ReadAllText(codingRuntimeStateControllerSetPath) : "";
        var codingSchemaStateControllerSet = File.Exists(codingSchemaStateControllerSetPath) ? File.ReadAllText(codingSchemaStateControllerSetPath) : "";
        var codingAiStateControllerSet = File.Exists(codingAiStateControllerSetPath) ? File.ReadAllText(codingAiStateControllerSetPath) : "";
        var importEventsOwner = File.Exists(importEventsOwnerPath) ? File.ReadAllText(importEventsOwnerPath) : "";
        var protocolStateControllerSet = File.Exists(protocolStateControllerSetPath) ? File.ReadAllText(protocolStateControllerSetPath) : "";
        var overlayStateControllerSet = File.Exists(overlayStateControllerSetPath) ? File.ReadAllText(overlayStateControllerSetPath) : "";
        var sidePanelControllerSet = File.Exists(sidePanelControllerSetPath) ? File.ReadAllText(sidePanelControllerSetPath) : "";
        var accessors = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.CodingSidePanelAccessors.cs"));
        var sidePanelEventBinder = File.Exists(sidePanelEventBinderPath) ? File.ReadAllText(sidePanelEventBinderPath) : "";
        var sidePanelControllerInitializer = File.Exists(sidePanelControllerInitializerPath) ? File.ReadAllText(sidePanelControllerInitializerPath) : "";

        Assert.DoesNotContain("private bool _isCodingMode", coding);
        Assert.DoesNotContain("private CodingSessionViewModel? _codingVm", coding);
        Assert.DoesNotContain("private enum EingabemarkerPhase", coding);
        Assert.DoesNotContain("private readonly ObservableCollection<CodingEvent> _codingImportEvents", coding);
        Assert.DoesNotContain("private bool _isCodingMode", state);
        Assert.Contains("private readonly CodingRuntimeStateControllerSet _codingRuntimeStates = new();", state);
        Assert.DoesNotContain("private readonly CodingModeStateController _codingModeState = new();", state);
        Assert.DoesNotContain("private readonly CodingSessionServiceOwner _codingSessionRuntimeOwner = new();", state);
        Assert.DoesNotContain("private readonly CodingOverlayServiceOwner _codingOverlayRuntimeOwner = new();", state);
        Assert.Contains("private CodingModeStateController _codingModeState => _codingRuntimeStates.ModeState", state);
        Assert.Contains("private CodingSessionServiceOwner _codingSessionRuntimeOwner => _codingRuntimeStates.SessionRuntimeOwner", state);
        Assert.Contains("private CodingOverlayServiceOwner _codingOverlayRuntimeOwner => _codingRuntimeStates.OverlayRuntimeOwner", state);
        Assert.Contains("public sealed class CodingRuntimeStateControllerSet", codingRuntimeStateControllerSet);
        Assert.Contains("public CodingModeStateController ModeState", codingRuntimeStateControllerSet);
        Assert.Contains("public CodingSessionServiceOwner SessionRuntimeOwner", codingRuntimeStateControllerSet);
        Assert.Contains("public CodingOverlayServiceOwner OverlayRuntimeOwner", codingRuntimeStateControllerSet);
        Assert.Contains("public sealed class CodingModeStateController", codingModeState);
        Assert.Contains("public bool IsCodingMode", codingModeState);
        Assert.Contains("public void Set", codingModeState);
        Assert.Contains("private readonly CodingSchemaStateControllerSet _codingSchemaStates = new();", state);
        Assert.DoesNotContain("private readonly CodingSchemaOverlayManagerOwner _codingSchemaManager = new();", state);
        Assert.DoesNotContain("private readonly CodingSchemaTypeStateController _codingSchemaTypeState = new();", state);
        Assert.Contains("private CodingSchemaOverlayManagerOwner _codingSchemaManager => _codingSchemaStates.OverlayManagerOwner", state);
        Assert.Contains("private CodingSchemaTypeStateController _codingSchemaTypeState => _codingSchemaStates.TypeState", state);
        Assert.Contains("public sealed class CodingSchemaStateControllerSet", codingSchemaStateControllerSet);
        Assert.Contains("public CodingSchemaOverlayManagerOwner OverlayManagerOwner", codingSchemaStateControllerSet);
        Assert.Contains("public CodingSchemaTypeStateController TypeState", codingSchemaStateControllerSet);
        Assert.Contains("private readonly CodingSessionViewModelOwner _codingSessionViewModelOwner", state);
        Assert.DoesNotContain("private CodingSessionViewModel? _codingVm", state);
        Assert.DoesNotContain("private ICodingSessionService? _codingSessionService", state);
        Assert.DoesNotContain("private enum EingabemarkerPhase", state);
        Assert.Contains("private readonly CodingAiStateControllerSet _codingAiStates = new();", state);
        Assert.DoesNotContain("private readonly LiveDetectionPulseStateController _codingAiPulseStateController = new();", state);
        Assert.DoesNotContain("private readonly CodingAiOverlayAutoHideTimerOwner _codingAiOverlayAutoHideTimerOwner = new();", state);
        Assert.DoesNotContain("private readonly CodingAiControllerOwner _codingAiRuntimeOwner = new();", state);
        Assert.DoesNotContain("private readonly CodingFrameReadinessController _codingFrameReadinessController = new();", state);
        Assert.DoesNotContain("private readonly CodingLiveAiTimerControllerOwner _codingLiveAiTimerOwner = new();", state);
        Assert.Contains("private LiveDetectionPulseStateController _codingAiPulseStateController => _codingAiStates.PulseState", state);
        Assert.Contains("private CodingAiOverlayAutoHideTimerOwner _codingAiOverlayAutoHideTimerOwner => _codingAiStates.OverlayAutoHideTimerOwner", state);
        Assert.Contains("private CodingAiControllerOwner _codingAiRuntimeOwner => _codingAiStates.RuntimeOwner", state);
        Assert.Contains("private CodingFrameReadinessController _codingFrameReadinessController => _codingAiStates.FrameReadinessController", state);
        Assert.Contains("private CodingLiveAiTimerControllerOwner _codingLiveAiTimerOwner => _codingAiStates.LiveTimerOwner", state);
        Assert.Contains("public sealed class CodingAiStateControllerSet", codingAiStateControllerSet);
        Assert.Contains("private readonly CodingOverlayStateControllerSet _codingOverlayStates = new();", state);
        Assert.DoesNotContain("private readonly CodingCalibrationStateController _codingCalibrationState = new();", state);
        Assert.DoesNotContain("private readonly CodingOverlayInputVisibilityStateController _codingOverlayInputVisibilityState = new();", state);
        Assert.DoesNotContain("private readonly CodingOverlayRenderStateController _codingOverlayRenderState = new();", state);
        Assert.DoesNotContain("private readonly CodingActiveToolNameStateController _codingActiveToolNameState = new();", state);
        Assert.Contains("private CodingCalibrationStateController _codingCalibrationState => _codingOverlayStates.CalibrationState", state);
        Assert.Contains("private CodingOverlayInputVisibilityStateController _codingOverlayInputVisibilityState => _codingOverlayStates.InputVisibilityState", state);
        Assert.Contains("private CodingOverlayRenderStateController _codingOverlayRenderState => _codingOverlayStates.RenderState", state);
        Assert.Contains("private CodingActiveToolNameStateController _codingActiveToolNameState => _codingOverlayStates.ActiveToolNameState", state);
        Assert.Contains("public sealed class CodingOverlayStateControllerSet", overlayStateControllerSet);
        Assert.Contains("private readonly CodingEingabemarkerStateController _eingabemarkerState = new();", state);
        Assert.DoesNotContain("private readonly ObservableCollection<CodingEvent> _codingImportEvents", state);
        Assert.Contains("private readonly CodingProtocolStateControllerSet _codingProtocolStates = new();", state);
        Assert.DoesNotContain("private readonly CodingImportReferenceEventsOwner _codingImportReferenceEvents = new();", state);
        Assert.DoesNotContain("private readonly CodingNavigationPendingState _codingNavigationPendingState = new();", state);
        Assert.DoesNotContain("private readonly CodingProtocolMatchStateController _codingProtocolMatchState = new();", state);
        Assert.DoesNotContain("private readonly CodingPendingConfirmationStateController _codingPendingConfirmationState = new();", state);
        Assert.DoesNotContain("private readonly CodingBaselineSignatureStateController _codingBaselineSignatureState = new();", state);
        Assert.Contains("private CodingImportReferenceEventsOwner _codingImportReferenceEvents => _codingProtocolStates.ImportReferenceEvents", state);
        Assert.Contains("private CodingNavigationPendingState _codingNavigationPendingState => _codingProtocolStates.NavigationPendingState", state);
        Assert.Contains("private CodingProtocolMatchStateController _codingProtocolMatchState => _codingProtocolStates.ProtocolMatchState", state);
        Assert.Contains("private CodingPendingConfirmationStateController _codingPendingConfirmationState => _codingProtocolStates.PendingConfirmationState", state);
        Assert.Contains("private CodingBaselineSignatureStateController _codingBaselineSignatureState => _codingProtocolStates.BaselineSignatureState", state);
        Assert.Contains("public sealed class CodingProtocolStateControllerSet", protocolStateControllerSet);
        Assert.Contains("public sealed class CodingImportReferenceEventsOwner", importEventsOwner);
        Assert.Contains("public ObservableCollection<CodingEvent> Events", importEventsOwner);
        Assert.DoesNotContain("private CodingEventsListControls _codingEventsListControls", state);
        Assert.DoesNotContain("private CodingStatisticsControls _codingStatisticsControls", state);
        Assert.DoesNotContain("private CodingInlineDefectDetailControls _codingInlineDefectDetailControls", state);
        Assert.DoesNotContain("private CodingEventCreationPostActions _codingEventCreationPostActions", state);
        Assert.Contains("private readonly CodingSidePanelControllerSet _codingSidePanelControllers = new();", state);
        Assert.Contains("public sealed class CodingSidePanelControllerSet", sidePanelControllerSet);
        Assert.Contains("public void Initialize", sidePanelControllerSet);
        Assert.Contains("PlayerCodingSidePanelControllerInitializer.Initialize", accessors);
        Assert.DoesNotContain("new CodingSidePanelControllerControls(", accessors);
        Assert.Contains("new CodingSidePanelControllerControls(", sidePanelControllerInitializer);
        Assert.Contains("sidePanel.LstCodingEvents", sidePanelControllerInitializer);
        Assert.Contains("PlayerCodingSidePanelEventBinder.Bind", accessors);
        Assert.DoesNotContain("CodingSidePanelControl.CodingTakePhotoRequested +=", accessors);
        Assert.DoesNotContain("CodingSidePanelControl.CodingProtocolMatchRequested +=", accessors);
        Assert.Contains("sidePanel.CodingProtocolMatchRequested +=", sidePanelEventBinder);
        Assert.DoesNotContain("using AuswertungPro.Next.Application", state);
        Assert.DoesNotContain("using AuswertungPro.Next.Domain", state);
        Assert.DoesNotContain("using AuswertungPro.Next.Infrastructure", state);
        Assert.DoesNotContain("using AuswertungPro.Next.UI.Ai", state);
        Assert.Contains("using AuswertungPro.Next.UI.Player;", state);
    }

    [Fact]
    public void PlayerWindow_coding_schema_type_state_lives_in_state_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var schemaStatePath = Path.Combine(uiRoot, "Player", "CodingSchemaTypeStateController.cs");
        var schemaStateSetPath = Path.Combine(uiRoot, "Player", "CodingSchemaStateControllerSet.cs");

        Assert.True(File.Exists(schemaStatePath), "Aktiver Schema-Typ soll nicht mehr als Rohfeld im PlayerWindow liegen.");
        Assert.True(File.Exists(schemaStateSetPath), "Schema-Zustand soll gebuendelt im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var schemaState = File.Exists(schemaStatePath) ? File.ReadAllText(schemaStatePath) : "";
        var schemaStateSet = File.Exists(schemaStateSetPath) ? File.ReadAllText(schemaStateSetPath) : "";

        Assert.DoesNotContain("private SchemaType? _codingSchemaType;", state);
        Assert.DoesNotContain("private readonly CodingSchemaTypeStateController _codingSchemaTypeState = new();", state);
        Assert.Contains("private CodingSchemaTypeStateController _codingSchemaTypeState => _codingSchemaStates.TypeState", state);
        Assert.Contains("public CodingSchemaTypeStateController TypeState", schemaStateSet);
        Assert.Contains("public sealed class CodingSchemaTypeStateController", schemaState);
        Assert.Contains("public SchemaType? ActiveSchemaType", schemaState);
        Assert.Contains("public void Set", schemaState);
        Assert.Contains("public void Clear", schemaState);
    }

    [Fact]
    public void PlayerWindow_coding_baseline_signature_lives_in_state_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var baselineStatePath = Path.Combine(uiRoot, "Player", "CodingBaselineSignatureStateController.cs");

        Assert.True(File.Exists(baselineStatePath), "Coding-Baseline-Signatur soll nicht mehr als Rohfeld im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var baselineState = File.Exists(baselineStatePath) ? File.ReadAllText(baselineStatePath) : "";

        Assert.DoesNotContain("private string _codingBaselineSignature = string.Empty;", state);
        Assert.Contains("private CodingBaselineSignatureStateController _codingBaselineSignatureState => _codingProtocolStates.BaselineSignatureState", state);
        Assert.Contains("public sealed class CodingBaselineSignatureStateController", baselineState);
        Assert.Contains("public string BaselineSignature", baselineState);
        Assert.Contains("public void Set", baselineState);
    }

    [Fact]
    public void PlayerWindow_coding_pending_confirmation_lives_in_state_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var pendingStatePath = Path.Combine(uiRoot, "Player", "CodingPendingConfirmationStateController.cs");

        Assert.True(File.Exists(pendingStatePath), "Coding-Pending-Confirmation soll nicht mehr als zwei Rohfelder im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var pendingState = File.Exists(pendingStatePath) ? File.ReadAllText(pendingStatePath) : "";

        Assert.DoesNotContain("private CodingEvent? _codingPendingConfirmEvent;", state);
        Assert.DoesNotContain("private QualityGateResult? _codingPendingGateResult;", state);
        Assert.Contains("private CodingPendingConfirmationStateController _codingPendingConfirmationState => _codingProtocolStates.PendingConfirmationState", state);
        Assert.Contains("public sealed class CodingPendingConfirmationStateController", pendingState);
        Assert.Contains("public CodingEvent? CodingEvent", pendingState);
        Assert.Contains("public QualityGateResult? GateResult", pendingState);
        Assert.Contains("public void Store", pendingState);
        Assert.Contains("public void Clear", pendingState);
    }

    [Fact]
    public void PlayerWindow_coding_protocol_match_state_lives_in_state_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var protocolMatchPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.cs");
        var highlightPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Highlighting.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var exitPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var importReferencePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.ImportReference.cs");
        var protocolStatePath = Path.Combine(uiRoot, "Player", "CodingProtocolMatchStateController.cs");

        Assert.True(File.Exists(protocolStatePath), "Coding-Protocol-Match-State soll nicht mehr als Rohfelder im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var highlight = File.ReadAllText(highlightPath);
        var training = File.ReadAllText(trainingPath);
        var exit = File.ReadAllText(exitPath);
        var importReference = File.ReadAllText(importReferencePath);
        var protocolState = File.Exists(protocolStatePath) ? File.ReadAllText(protocolStatePath) : "";

        Assert.DoesNotContain("private CodingMatchRouting? _lastCodingMatch;", state);
        Assert.DoesNotContain("private readonly Dictionary<Guid, CodingProtocolMatchBucket> _codingProtocolMatchBuckets", state);
        Assert.Contains("private CodingProtocolMatchStateController _codingProtocolMatchState => _codingProtocolStates.ProtocolMatchState", state);
        Assert.Contains("_codingProtocolMatchState.Buckets", protocolMatch);
        Assert.Contains("StoreMatch: _codingProtocolMatchState.Store", protocolMatch);
        Assert.Contains("_codingProtocolMatchState.TryGetBucket", highlight);
        Assert.Contains("_codingProtocolMatchState.LastMatch", training);
        Assert.Contains("_codingProtocolMatchState.Reset", exit);
        Assert.Contains("_codingProtocolMatchState.Reset", importReference);
        Assert.Contains("public sealed class CodingProtocolMatchStateController", protocolState);
        Assert.Contains("public CodingMatchRouting? LastMatch", protocolState);
        Assert.Contains("public IDictionary<Guid, CodingProtocolMatchBucket> Buckets", protocolState);
        Assert.Contains("public void Store", protocolState);
        Assert.Contains("public CodingMatchRouting? Reset", protocolState);
        Assert.Contains("public bool TryGetBucket", protocolState);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_state_lives_in_state_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var markerPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Eingabemarker.cs");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var submissionPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Eingabemarker.Submission.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingEingabemarkerStateController.cs");

        Assert.True(File.Exists(controllerPath), "Eingabemarker-Zustand soll nicht mehr als Rohfelder im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var marker = File.ReadAllText(markerPath);
        var overlayInput = File.ReadAllText(overlayInputPath);
        var submission = File.ReadAllText(submissionPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";

        Assert.DoesNotContain("private enum EingabemarkerPhase", state);
        Assert.DoesNotContain("_eingabemarkerPhase", state + marker + overlayInput + submission);
        Assert.DoesNotContain("_eingabemarkerDragStart", state + marker + overlayInput + submission);
        Assert.DoesNotContain("_eingabemarkerRectNorm", state + marker + overlayInput + submission);
        Assert.DoesNotContain("_eingabemarkerPreviewRect", state + marker + overlayInput + submission);
        Assert.Contains("private readonly CodingEingabemarkerStateController _eingabemarkerState = new();", state);
        Assert.Contains("public sealed class CodingEingabemarkerStateController", controller);
        Assert.Contains("public CodingEingabemarkerPhase Phase", controller);
        Assert.Contains("public Point DragStart", controller);
        Assert.Contains("public Rect NormalizedSelection", controller);
        Assert.Contains("public System.Windows.Shapes.Rectangle? PreviewRect", controller);
        Assert.Contains("public CodingOverlayInputEingabemarkerState OverlayInputState", controller);
    }
}
