using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Sanierung;
using AuswertungPro.Next.UI.Ai.Sanierung.Dto;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class SanierungOptimizationViewModel : ObservableObject
{
    private readonly HaltungRecord _record;
    private readonly AiSanierungOptimizationService _aiService;
    private readonly SanierungOptimizationRequest _request;

    // ── Observable properties ─────────────────────────────────────────────

    [ObservableProperty] private string _haltungName  = "";
    [ObservableProperty] private string _statusText   = "";
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private bool   _hasResult;
    [ObservableProperty] private bool   _hasError;

    [ObservableProperty] private SanierungOptimizationResult? _result;

    // Rule block
    [ObservableProperty] private string _ruleMeasures      = "";
    [ObservableProperty] private string _ruleEstimatedCost = "";

    // AI block
    [ObservableProperty] private string _aiMeasure    = "";
    [ObservableProperty] private string _aiReasoning  = "";
    [ObservableProperty] private double _aiConfidence;
    [ObservableProperty] private string _costMin      = "";
    [ObservableProperty] private string _costExpected = "";
    [ObservableProperty] private string _costMax      = "";
    [ObservableProperty] private string _riskText     = "";
    [ObservableProperty] private bool   _isFallback;

    // ── Events ────────────────────────────────────────────────────────────

    public event Action? CloseRequested;
    public event Action<SanierungOptimizationResult, AiOptimizationSession>? AppliedToSecondary;
    public event Action<SanierungOptimizationResult>? TransferredToPrimary;

    // ── Constructor ───────────────────────────────────────────────────────

    public SanierungOptimizationViewModel(
        HaltungRecord record,
        AiSanierungOptimizationService aiService,
        RuleRecommendationDto? ruleRecommendation)
    {
        _record    = record;
        _aiService = aiService;
        _request   = BuildRequest(record, ruleRecommendation);

        HaltungName = record.GetFieldValue("Haltungsname") ?? record.Id.ToString();
        StatusText  = "Bereit – klicke auf 'Optimieren', um die KI zu starten.";

        // Pre-fill rule block
        if (ruleRecommendation is not null && ruleRecommendation.Measures.Count > 0)
        {
            RuleMeasures      = string.Join(", ", ruleRecommendation.Measures);
            RuleEstimatedCost = ruleRecommendation.EstimatedCost.HasValue
                ? ruleRecommendation.EstimatedCost.Value.ToString("0.00", CultureInfo.InvariantCulture) + " CHF"
                : "–";
        }
        else
        {
            RuleMeasures      = "Keine Regelempfehlung verfügbar";
            RuleEstimatedCost = "–";
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanOptimize))]
    private async Task OptimizeAsync()
    {
        IsBusy     = true;
        HasError   = false;
        HasResult  = false;
        StatusText = "KI-Optimierung läuft…";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var result = await _aiService.OptimizeAsync(_request, cts.Token).ConfigureAwait(false);

            Result        = result;
            AiMeasure     = result.RecommendedMeasure;
            AiConfidence  = result.Confidence;
            AiReasoning   = result.Reasoning;
            CostMin       = result.CostEstimate.Min.ToString("0", CultureInfo.InvariantCulture);
            CostExpected  = result.CostEstimate.Expected.ToString("0", CultureInfo.InvariantCulture);
            CostMax       = result.CostEstimate.Max.ToString("0", CultureInfo.InvariantCulture);
            RiskText      = result.RiskFlags.Count > 0
                ? string.Join("\n", result.RiskFlags)
                : "";
            IsFallback    = result.IsFallback;
            HasResult     = true;
            HasError      = result.Error is not null;
            StatusText    = result.IsFallback
                ? "Regelbasierter Fallback aktiv (KI-Vorschlag verworfen)"
                : $"KI-Optimierung abgeschlossen (Signale: {result.UsedSignals})";
        }
        catch (Exception ex)
        {
            HasError   = true;
            StatusText = "Fehler: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            OptimizeCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanOptimize() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyToSecondaryAsync()
    {
        if (Result is null) return;

        var session = BuildSession(UserDecision.Accepted);
        await AiOptimizationSessionStore.SaveAsync(session).ConfigureAwait(false);

        AppliedToSecondary?.Invoke(Result, session);
        StatusText = "KI-Vorschlag als Sekundärdaten gespeichert.";
    }

    private bool CanApply() => HasResult && Result is not null;

    [RelayCommand(CanExecute = nameof(CanTransfer))]
    private async Task TransferToPrimaryAsync()
    {
        if (Result is null) return;

        // Write session with Accepted decision
        var session = BuildSession(UserDecision.Accepted);
        session.FinalAppliedMeasure = Result.RecommendedMeasure;
        await AiOptimizationSessionStore.SaveAsync(session).ConfigureAwait(false);

        // Apply to HaltungRecord fields
        _record.SetFieldValue("Empfohlene_Sanierungsmassnahmen",
            Result.RecommendedMeasure, FieldSource.Unknown, userEdited: false);
        _record.SetFieldValue("Kosten",
            Result.CostEstimate.Expected.ToString("0.00", CultureInfo.InvariantCulture),
            FieldSource.Unknown, userEdited: false);

        if (!string.IsNullOrWhiteSpace(Result.Reasoning))
            _record.SetFieldValue("Bemerkungen",
                $"[KI-Vorschlag] {Result.Reasoning}", FieldSource.Unknown, userEdited: false);

        TransferredToPrimary?.Invoke(Result);
        StatusText = "KI-Vorschlag in Haltungsdaten übertragen.";
        CloseRequested?.Invoke();
    }

    private bool CanTransfer() => HasResult && Result is not null;

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private AiOptimizationSession BuildSession(UserDecision decision)
    {
        var jsonOpts = new JsonSerializerOptions { WriteIndented = false };
        return new AiOptimizationSession
        {
            HaltungId        = HaltungName,
            InputSnapshot    = JsonSerializer.Serialize(_request, jsonOpts),
            RuleSnapshot     = JsonSerializer.Serialize(_request.Rule, jsonOpts),
            AiResultSnapshot = Result is not null ? JsonSerializer.Serialize(Result, jsonOpts) : "",
            Decision         = decision,
            FinalAppliedMeasure = Result?.RecommendedMeasure
        };
    }

    private static SanierungOptimizationRequest BuildRequest(
        HaltungRecord record,
        RuleRecommendationDto? rule)
    {
        // Map VsaFindings → DamageFindingDto
        var findings = new List<DamageFindingDto>();

        if (record.Protocol?.Current?.Entries is { Count: > 0 } entries)
        {
            foreach (var e in entries.Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code)))
            {
                findings.Add(new DamageFindingDto
                {
                    Code          = e.Code,
                    PositionMeter = e.MeterStart,
                    LengthMeter   = e.MeterEnd.HasValue && e.MeterStart.HasValue
                        ? e.MeterEnd.Value - e.MeterStart.Value
                        : null,
                    Comment = e.Beschreibung
                });
            }
        }
        else if (record.VsaFindings is { Count: > 0 } vsaFindings)
        {
            foreach (var f in vsaFindings)
            {
                findings.Add(new DamageFindingDto
                {
                    Code          = f.KanalSchadencode?.Trim() ?? "",
                    PositionMeter = f.MeterStart ?? f.SchadenlageAnfang,
                    Quantification = f.Quantifizierung1
                });
            }
        }

        // Map pipe metadata
        var dnRaw  = record.GetFieldValue("DN_mm");
        int.TryParse(dnRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dn);

        var lengthRaw = record.GetFieldValue("Haltungslaenge_m");
        double.TryParse(lengthRaw?.Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var lengthM);

        var pipe = new PipeContextDto
        {
            DiameterMm  = dn > 0 ? dn : null,
            Material    = record.GetFieldValue("Rohrmaterial"),
            LengthMeter = lengthM > 0 ? lengthM : null,
            Region      = record.GetFieldValue("Strasse"),
            ProjectYear = DateTime.UtcNow.Year
        };

        // Attach zustandsklasse to first finding's SeverityClass for validation
        var zk = record.GetFieldValue("Zustandsklasse");
        if (!string.IsNullOrWhiteSpace(zk) && findings.Count > 0)
        {
            findings[0] = findings[0] with { SeverityClass = zk };
        }

        // Build a rule DTO from MeasureRecommendationResult
        var ruleDto = rule;

        return new SanierungOptimizationRequest
        {
            HaltungId = record.GetFieldValue("Haltungsname") ?? record.Id.ToString(),
            Findings  = findings,
            Pipe      = pipe,
            Rule      = ruleDto
        };
    }
}
