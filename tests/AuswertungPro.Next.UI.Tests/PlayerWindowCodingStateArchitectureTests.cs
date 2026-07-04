using System.Collections.Generic;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingStateArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_state_fields_live_in_coding_state_partial()
    {
        var codingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var codingModeStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingModeStateController.cs");
        var codingRuntimeStateControllerSetPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingRuntimeStateControllerSet.cs");
        var codingSchemaStateControllerSetPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSchemaStateControllerSet.cs");
        var codingAiStateControllerSetPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingAiStateControllerSet.cs");
        var importEventsOwnerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingImportReferenceEventsOwner.cs");
        var protocolStateControllerSetPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingProtocolStateControllerSet.cs");
        var overlayStateControllerSetPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingOverlayStateControllerSet.cs");
        var sidePanelControllerSetPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSidePanelControllerSet.cs");
        var sidePanelEventBinderPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerCodingSidePanelEventBinder.cs");
        var sidePanelControllerInitializerPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerCodingSidePanelControllerInitializer.cs");

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
        var accessors = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs"));
        var sidePanelEventBinder = File.Exists(sidePanelEventBinderPath) ? File.ReadAllText(sidePanelEventBinderPath) : "";
        var sidePanelControllerInitializer = File.Exists(sidePanelControllerInitializerPath) ? File.ReadAllText(sidePanelControllerInitializerPath) : "";

        AssertNoForbiddenTokens(
            coding,
            "private bool _isCodingMode",
            "private enum EingabemarkerPhase",
            "private readonly ObservableCollection<CodingEvent> _codingImportEvents");
        AssertNoForbiddenTokens(
            state,
            "private bool _isCodingMode",
            "private readonly CodingModeStateController _codingModeState = new();",
            "private readonly CodingSessionServiceOwner _codingSessionRuntimeOwner = new();",
            "private readonly CodingOverlayServiceOwner _codingOverlayRuntimeOwner = new();",
            "private readonly CodingSchemaOverlayManagerOwner _codingSchemaManager = new();",
            "private ICodingSessionService? _codingSessionService",
            "private readonly LiveDetectionPulseStateController _codingAiPulseStateController = new();",
            "private readonly CodingAiOverlayAutoHideTimerOwner _codingAiOverlayAutoHideTimerOwner = new();",
            "private readonly CodingAiControllerOwner _codingAiRuntimeOwner = new();",
            "private readonly CodingFrameReadinessController _codingFrameReadinessController = new();",
            "private readonly CodingLiveAiTimerControllerOwner _codingLiveAiTimerOwner = new();",
            "private readonly CodingCalibrationStateController _codingCalibrationState = new();",
            "private readonly CodingOverlayInputVisibilityStateController _codingOverlayInputVisibilityState = new();",
            "private readonly CodingOverlayRenderStateController _codingOverlayRenderState = new();",
            "private readonly CodingActiveToolNameStateController _codingActiveToolNameState = new();",
            "private readonly ObservableCollection<CodingEvent> _codingImportEvents",
            "private readonly CodingImportReferenceEventsOwner _codingImportReferenceEvents = new();",
            "private readonly CodingNavigationPendingState _codingNavigationPendingState = new();",
            "private readonly CodingProtocolMatchStateController _codingProtocolMatchState = new();",
            "private readonly CodingPendingConfirmationStateController _codingPendingConfirmationState = new();",
            "private readonly CodingBaselineSignatureStateController _codingBaselineSignatureState = new();",
            "private CodingEventsListControls _codingEventsListControls",
            "private CodingStatisticsControls _codingStatisticsControls",
            "private CodingInlineDefectDetailControls _codingInlineDefectDetailControls",
            "private CodingEventCreationPostActions _codingEventCreationPostActions",
            "using AuswertungPro.Next.Application",
            "using AuswertungPro.Next.Domain",
            "using AuswertungPro.Next.Infrastructure",
            "using AuswertungPro.Next.UI.Ai");
        Assert.Contains("private readonly CodingRuntimeStateControllerSet _codingRuntimeStates = new();", state);
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
        Assert.Contains("private CodingSchemaOverlayManagerOwner _codingSchemaManager => _codingSchemaStates.OverlayManagerOwner", state);
        Assert.Contains("private CodingSchemaTypeStateController _codingSchemaTypeState => _codingSchemaStates.TypeState", state);
        Assert.Contains("public sealed class CodingSchemaStateControllerSet", codingSchemaStateControllerSet);
        Assert.Contains("public CodingSchemaOverlayManagerOwner OverlayManagerOwner", codingSchemaStateControllerSet);
        Assert.Contains("public CodingSchemaTypeStateController TypeState", codingSchemaStateControllerSet);
        Assert.Contains("private readonly CodingSessionViewModelOwner _codingSessionViewModelOwner", state);
        Assert.Contains("private readonly CodingAiStateControllerSet _codingAiStates = new();", state);
        Assert.Contains("private LiveDetectionPulseStateController _codingAiPulseStateController => _codingAiStates.PulseState", state);
        Assert.Contains("private CodingAiOverlayAutoHideTimerOwner _codingAiOverlayAutoHideTimerOwner => _codingAiStates.OverlayAutoHideTimerOwner", state);
        Assert.Contains("private CodingAiControllerOwner _codingAiRuntimeOwner => _codingAiStates.RuntimeOwner", state);
        Assert.Contains("private CodingFrameReadinessController _codingFrameReadinessController => _codingAiStates.FrameReadinessController", state);
        Assert.Contains("private CodingLiveAiTimerControllerOwner _codingLiveAiTimerOwner => _codingAiStates.LiveTimerOwner", state);
        Assert.Contains("public sealed class CodingAiStateControllerSet", codingAiStateControllerSet);
        Assert.Contains("private readonly CodingOverlayStateControllerSet _codingOverlayStates = new();", state);
        Assert.Contains("private CodingCalibrationStateController _codingCalibrationState => _codingOverlayStates.CalibrationState", state);
        Assert.Contains("private CodingOverlayInputVisibilityStateController _codingOverlayInputVisibilityState => _codingOverlayStates.InputVisibilityState", state);
        Assert.Contains("private CodingOverlayRenderStateController _codingOverlayRenderState => _codingOverlayStates.RenderState", state);
        Assert.Contains("private CodingActiveToolNameStateController _codingActiveToolNameState => _codingOverlayStates.ActiveToolNameState", state);
        Assert.Contains("public sealed class CodingOverlayStateControllerSet", overlayStateControllerSet);
        Assert.Contains("private readonly CodingEingabemarkerStateController _eingabemarkerState = new();", state);
        Assert.Contains("private readonly CodingProtocolStateControllerSet _codingProtocolStates = new();", state);
        Assert.Contains("private CodingImportReferenceEventsOwner _codingImportReferenceEvents => _codingProtocolStates.ImportReferenceEvents", state);
        Assert.Contains("private CodingNavigationPendingState _codingNavigationPendingState => _codingProtocolStates.NavigationPendingState", state);
        Assert.Contains("private CodingProtocolMatchStateController _codingProtocolMatchState => _codingProtocolStates.ProtocolMatchState", state);
        Assert.Contains("private CodingPendingConfirmationStateController _codingPendingConfirmationState => _codingProtocolStates.PendingConfirmationState", state);
        Assert.Contains("private CodingBaselineSignatureStateController _codingBaselineSignatureState => _codingProtocolStates.BaselineSignatureState", state);
        Assert.Contains("public sealed class CodingProtocolStateControllerSet", protocolStateControllerSet);
        Assert.Contains("public sealed class CodingImportReferenceEventsOwner", importEventsOwner);
        Assert.Contains("public ObservableCollection<CodingEvent> Events", importEventsOwner);
        Assert.Contains("private readonly CodingSidePanelControllerSet _codingSidePanelControllers = new();", state);
        Assert.Contains("public sealed class CodingSidePanelControllerSet", sidePanelControllerSet);
        Assert.Contains("public void Initialize", sidePanelControllerSet);
        Assert.Contains("PlayerCodingSidePanelControllerInitializer.Initialize", accessors);
        AssertNoForbiddenTokens(
            accessors,
            "new CodingSidePanelControllerControls(",
            "CodingSidePanelControl.CodingTakePhotoRequested +=",
            "CodingSidePanelControl.CodingProtocolMatchRequested +=");
        Assert.Contains("new CodingSidePanelControllerControls(", sidePanelControllerInitializer);
        Assert.Contains("sidePanel.LstCodingEvents", sidePanelControllerInitializer);
        Assert.Contains("PlayerCodingSidePanelEventBinder.Bind", accessors);
        Assert.Contains("sidePanel.CodingProtocolMatchRequested +=", sidePanelEventBinder);
        Assert.Contains("using AuswertungPro.Next.UI.Player;", state);
    }

    [Fact]
    public void PlayerWindow_coding_schema_type_state_lives_in_state_controller()
    {
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var schemaStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSchemaTypeStateController.cs");
        var schemaStateSetPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSchemaStateControllerSet.cs");

        Assert.True(File.Exists(schemaStatePath), "Aktiver Schema-Typ soll nicht mehr als Rohfeld im PlayerWindow liegen.");
        Assert.True(File.Exists(schemaStateSetPath), "Schema-Zustand soll gebuendelt im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var schemaState = File.Exists(schemaStatePath) ? File.ReadAllText(schemaStatePath) : "";
        var schemaStateSet = File.Exists(schemaStateSetPath) ? File.ReadAllText(schemaStateSetPath) : "";

        AssertNoForbiddenTokens(
            state,
            "private SchemaType? _codingSchemaType;",
            "private readonly CodingSchemaTypeStateController _codingSchemaTypeState = new();");
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
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var baselineStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingBaselineSignatureStateController.cs");

        Assert.True(File.Exists(baselineStatePath), "Coding-Baseline-Signatur soll nicht mehr als Rohfeld im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var baselineState = File.Exists(baselineStatePath) ? File.ReadAllText(baselineStatePath) : "";

        AssertNoForbiddenTokens(state, "private string _codingBaselineSignature = string.Empty;");
        Assert.Contains("private CodingBaselineSignatureStateController _codingBaselineSignatureState => _codingProtocolStates.BaselineSignatureState", state);
        Assert.Contains("public sealed class CodingBaselineSignatureStateController", baselineState);
        Assert.Contains("public string BaselineSignature", baselineState);
        Assert.Contains("public void Set", baselineState);
    }

    [Fact]
    public void PlayerWindow_coding_pending_confirmation_lives_in_state_controller()
    {
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var pendingStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingPendingConfirmationStateController.cs");

        Assert.True(File.Exists(pendingStatePath), "Coding-Pending-Confirmation soll nicht mehr als zwei Rohfelder im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var pendingState = File.Exists(pendingStatePath) ? File.ReadAllText(pendingStatePath) : "";

        AssertNoForbiddenTokens(
            state,
            "private CodingEvent? _codingPendingConfirmEvent;",
            "private QualityGateResult? _codingPendingGateResult;");
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
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var protocolMatchPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.cs");
        var highlightPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Highlighting.cs");
        var trainingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var exitPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var importReferencePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.ImportReference.cs");
        var protocolStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingProtocolMatchStateController.cs");

        Assert.True(File.Exists(protocolStatePath), "Coding-Protocol-Match-State soll nicht mehr als Rohfelder im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var highlight = File.ReadAllText(highlightPath);
        var training = File.ReadAllText(trainingPath);
        var exit = File.ReadAllText(exitPath);
        var importReference = File.ReadAllText(importReferencePath);
        var protocolState = File.Exists(protocolStatePath) ? File.ReadAllText(protocolStatePath) : "";

        AssertNoForbiddenTokens(
            state,
            "private CodingMatchRouting? _lastCodingMatch;",
            "private readonly Dictionary<Guid, CodingProtocolMatchBucket> _codingProtocolMatchBuckets");
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
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var markerPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var overlayInputPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var submissionPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.Submission.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingEingabemarkerStateController.cs");

        Assert.True(File.Exists(controllerPath), "Eingabemarker-Zustand soll nicht mehr als Rohfelder im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var marker = File.ReadAllText(markerPath);
        var overlayInput = File.ReadAllText(overlayInputPath);
        var submission = File.ReadAllText(submissionPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";

        AssertNoForbiddenTokens(
            state,
            "private enum EingabemarkerPhase");
        AssertNoForbiddenTokens(
            state + marker + overlayInput + submission,
            "_eingabemarkerPhase",
            "_eingabemarkerDragStart",
            "_eingabemarkerRectNorm",
            "_eingabemarkerPreviewRect");
        Assert.Contains("private readonly CodingEingabemarkerStateController _eingabemarkerState = new();", state);
        Assert.Contains("public sealed class CodingEingabemarkerStateController", controller);
        Assert.Contains("public CodingEingabemarkerPhase Phase", controller);
        Assert.Contains("public Point DragStart", controller);
        Assert.Contains("public Rect NormalizedSelection", controller);
        Assert.Contains("public System.Windows.Shapes.Rectangle? PreviewRect", controller);
        Assert.Contains("public CodingOverlayInputEingabemarkerState OverlayInputState", controller);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = new List<string>();
        foreach (var token in forbiddenTokens)
        {
            if (source.Contains(token, StringComparison.Ordinal))
                hits.Add(token);
        }

        Assert.True(
            hits.Count == 0,
            "Verbotene alte Coding-State-Logik gefunden: " + string.Join(", ", hits));
    }
}
