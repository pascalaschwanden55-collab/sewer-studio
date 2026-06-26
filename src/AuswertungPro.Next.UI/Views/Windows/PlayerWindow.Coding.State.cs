using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private bool _isCodingMode;
    private readonly CodingSessionServiceOwner _codingSessionRuntimeOwner = new();
    private readonly CodingOverlayServiceOwner _codingOverlayRuntimeOwner = new();
    private readonly SchemaOverlayManager _codingSchemaManager = new();
    private SchemaType? _codingSchemaType;

    private readonly CodingCalibrationStateController _codingCalibrationState = new();
    private readonly CodingOverlayInputVisibilityStateController _codingOverlayInputVisibilityState = new();
    private readonly CodingOverlayRenderStateController _codingOverlayRenderState = new();

    private readonly LiveDetectionPulseStateController _codingAiPulseStateController = new();
    private readonly StreckenschadenTracker _streckenTracker = new();
    private readonly CodingAiControllerOwner _codingAiRuntimeOwner = new();
    private readonly CodingFrameReadinessController _codingFrameReadinessController = new();
    private CodingLiveAiTimerController? _codingLiveAiTimers;
    private readonly CodingOsdMeterController _codingOsdMeterController = new();
    private CodingEventsListControls _codingEventsListControls = null!;
    private CodingStatisticsControls _codingStatisticsControls = null!;
    private CodingInlineDefectDetailControls _codingInlineDefectDetailControls = null!;
    private CodingEventCreationPostActions _codingEventCreationPostActions = null!;
    private CodingConfirmationPanelControls _codingConfirmationPanelControls = null!;
    private readonly CodingSessionViewModelOwner _codingSessionViewModelOwner;
    private readonly ICodingSessionHost _codingSessionHost;
    private readonly ICodingOverlayToolHost _codingOverlayToolHost;
    private readonly CodingNavigationPendingState _codingNavigationPendingState = new();

    private enum EingabemarkerPhase { Inactive, Drawing, Input, Analyzing }
    private EingabemarkerPhase _eingabemarkerPhase = EingabemarkerPhase.Inactive;
    private Point _eingabemarkerDragStart;
    private Rect _eingabemarkerRectNorm;
    private System.Windows.Shapes.Rectangle? _eingabemarkerPreviewRect;

    private readonly ObservableCollection<CodingEvent> _codingImportEvents = new();
    private CodingMatchRouting? _lastCodingMatch;
    private readonly Dictionary<Guid, CodingProtocolMatchBucket> _codingProtocolMatchBuckets = new();

    private CodingEvent? _codingPendingConfirmEvent;
    private QualityGateResult? _codingPendingGateResult;

    private string _codingBaselineSignature = string.Empty;
}
