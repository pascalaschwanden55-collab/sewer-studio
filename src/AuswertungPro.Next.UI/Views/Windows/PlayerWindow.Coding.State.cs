using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private readonly CodingModeStateController _codingModeState = new();
    private readonly CodingSessionServiceOwner _codingSessionRuntimeOwner = new();
    private readonly CodingOverlayServiceOwner _codingOverlayRuntimeOwner = new();
    private readonly SchemaOverlayManager _codingSchemaManager = new();
    private readonly CodingSchemaTypeStateController _codingSchemaTypeState = new();

    private readonly CodingCalibrationStateController _codingCalibrationState = new();
    private readonly CodingOverlayInputVisibilityStateController _codingOverlayInputVisibilityState = new();
    private readonly CodingOverlayRenderStateController _codingOverlayRenderState = new();
    private readonly CodingActiveToolNameStateController _codingActiveToolNameState = new();

    private readonly LiveDetectionPulseStateController _codingAiPulseStateController = new();
    private readonly StreckenschadenTracker _streckenTracker = new();
    private readonly CodingAiControllerOwner _codingAiRuntimeOwner = new();
    private readonly CodingFrameReadinessController _codingFrameReadinessController = new();
    private readonly CodingLiveAiTimerControllerOwner _codingLiveAiTimerOwner = new();
    private readonly CodingOsdMeterController _codingOsdMeterController = new();
    private readonly CodingSidePanelControllerSet _codingSidePanelControllers = new();
    private CodingConfirmationPanelControls _codingConfirmationPanelControls = null!;
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
