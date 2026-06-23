using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
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
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private bool _isCodingMode;
    private CodingSessionViewModel? _codingVm;
    private ICodingSessionService? _codingSessionService;
    private IOverlayToolService? _codingOverlayService;
    private readonly SchemaOverlayManager _codingSchemaManager = new();
    private SchemaType? _codingSchemaType;

    private bool _codingIsCalibrating;
    private NormalizedPoint? _codingCalibStart;
    private System.Windows.Shapes.Line? _codingPreviewLine;
    private bool _deactivatedByExternalWindow;
    private bool _showReferenceDn;

    private LiveDetectionService? _codingLiveDetection;
    private EnhancedVisionAnalysisService? _codingEnhancedVision;
    private CancellationTokenSource? _codingAnalysisCts;
    private bool _codingIsAnalyzing;
    private string _codingAiModelName = string.Empty;
    private bool _codingAiPulseRunning;
    private readonly StreckenschadenTracker _streckenTracker = new();
    private CodingLiveAiTimerController? _codingLiveAiTimers;
    private QualityGateService? _codingQualityGate;

    private enum EingabemarkerPhase { Inactive, Drawing, Input, Analyzing }
    private EingabemarkerPhase _eingabemarkerPhase = EingabemarkerPhase.Inactive;
    private Point _eingabemarkerDragStart;
    private Rect _eingabemarkerRectNorm;
    private System.Windows.Shapes.Rectangle? _eingabemarkerPreviewRect;

    private SingleFrameMultiModelService? _codingMultiModel;
    private IVisionPipelineClient? _codingVisionClient;
    private MarkBoxSegmentationService? _codingBoxSegmentation;
    private PipelineConfig? _codingPipelineConfig;
    private bool _codingUseMultiModel;
    private IPipelineHealthMonitor? _codingHealthMonitor;
    private bool _codingAiEnabled;

    private readonly ObservableCollection<CodingEvent> _codingImportEvents = new();
    private CodingMatchRouting? _lastCodingMatch;
    private readonly Dictionary<Guid, CodingProtocolMatchBucket> _codingProtocolMatchBuckets = new();

    private CodingEvent? _codingPendingConfirmEvent;
    private QualityGateResult? _codingPendingGateResult;

    private DispatcherTimer? _codingOsdTimer;
    private bool _codingOsdReading;
    private int _codingOverlaySuspendDepth;
    private bool _codingOverlayWasOpenBeforeSuspend;
    private bool _codingOverlayWasOpenBeforeExternalHide;
    private string _codingBaselineSignature = string.Empty;
    private readonly CodingFrameReadinessTracker _codingFrameReadiness = new();
}
