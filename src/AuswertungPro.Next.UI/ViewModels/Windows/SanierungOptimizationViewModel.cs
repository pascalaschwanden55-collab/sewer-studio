using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Application.Ai.Sanierung;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class SanierungOptimizationViewModel : ObservableObject
{
    private readonly HaltungRecord _record;
    private readonly IAiSanierungOptimizationService _aiService;
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
        IAiSanierungOptimizationService aiService,
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

        using var _aiToken = AiActivityTracker.Begin("Sanierungs-Optimierung");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var result = await _aiService.OptimizeAsync(_request, cts.Token);

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
        await AiOptimizationSessionStore.SaveAsync(session);

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
        await AiOptimizationSessionStore.SaveAsync(session);

        // Apply to HaltungRecord fields
        _record.SetFieldValue("Empfohlene_Sanierungsmassnahmen",
            Result.RecommendedMeasure, FieldSource.Unknown, userEdited: false);
        // KI-Kosten als Schaetzwert kennzeichnen: Suffix "(KI-Schaetzung)" im Bemerkungsfeld,
        // damit bei spaeterem Lesen klar ist dass der Wert nicht aus dem Kalkulator stammt.
        _record.SetFieldValue("Kosten",
            Result.CostEstimate.Expected.ToString("0.00", CultureInfo.InvariantCulture),
            FieldSource.Unknown, userEdited: false);
        // Bemerkung mit KI-Vorschlag UND Kosten-Hinweis
        var costNote = $"Kosten {Result.CostEstimate.Expected:N0} CHF = KI-Schaetzung (nicht kalkuliert), Bandbreite {Result.CostEstimate.Min:N0}–{Result.CostEstimate.Max:N0} CHF";
        var reasoning = !string.IsNullOrWhiteSpace(Result.Reasoning) ? Result.Reasoning : "";
        _record.SetFieldValue("Bemerkungen",
            $"[KI-Vorschlag] {reasoning}\n{costNote}", FieldSource.Unknown, userEdited: false);

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
        return new AiOptimizationSession
        {
            HaltungId        = HaltungName,
            InputSnapshot    = JsonSerializer.Serialize(_request),
            RuleSnapshot     = JsonSerializer.Serialize(_request.Rule),
            AiResultSnapshot = Result is not null ? JsonSerializer.Serialize(Result) : "",
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

        // Grundwasser aus Grundwasserspiegel-Feld: "oberhalb" = Grundwasser vorhanden
        var gwRaw = record.GetFieldValue("Grundwasserspiegel");
        bool? groundwater = gwRaw?.Trim().ToLowerInvariant() switch
        {
            "oberhalb" => true,
            "unterhalb" => false,
            _ => null
        };

        var pipe = new PipeContextDto
        {
            DiameterMm  = dn > 0 ? dn : null,
            Material    = record.GetFieldValue("Rohrmaterial"),
            LengthMeter = lengthM > 0 ? lengthM : null,
            Groundwater = groundwater,
            Region      = record.GetFieldValue("Strasse"),
            ProjectYear = DateTime.UtcNow.Year
        };

        // Zustandsklasse auf ALLE Findings verteilen (nicht nur erstes)
        var zk = record.GetFieldValue("Zustandsklasse");
        if (!string.IsNullOrWhiteSpace(zk))
        {
            for (int i = 0; i < findings.Count; i++)
                findings[i] = findings[i] with { SeverityClass = findings[i].SeverityClass ?? zk };
        }

        // Build a rule DTO from MeasureRecommendationResult
        var ruleDto = rule;

        // Marktreferenz aus historischen Profil-Aggregaten (Buerglen 2024-2026)
        MarktReferenzDto? marktRef = null;
        try
        {
            // Phase 5.1.B Etappe 3.E: via DI-Container.
            var historische = App.Resolve<Infrastructure.Devis.HistorischeSanierungenService>();
            var submissions = App.Resolve<Infrastructure.Devis.SubmissionsPositionService>();
            var dnVal = (double?)dn > 0 ? (double?)dn : null;
            var profile = historische.FindMatchingProfile(
                dnVal, pipe.Material, record.GetFieldValue("Nutzungsart"));
            if (profile is { AnzahlFaelle: >= 3 })
            {
                // VSA-Code des ersten Findings -> empfohlene Submissions-Blocks
                var firstCode = findings.Select(f => f.Code).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
                var blocks = !string.IsNullOrWhiteSpace(firstCode)
                    ? submissions.RecommendBlocksForVsaCode(firstCode!)
                    : Array.Empty<string>();

                marktRef = new MarktReferenzDto
                {
                    ProfilLabel = $"{profile.DnKlasse}, {profile.Material}, {profile.Nutzungsart}",
                    AnzahlFaelle = profile.AnzahlFaelle,
                    KostenProMMedianChf = (decimal?)profile.KostenProMMedianChf,
                    KostenProHaltungMedianChf = (decimal?)profile.KostenProHaltungMedianChf,
                    TypischeMassnahmen = profile.TypischeMassnahmen,
                    EmpfohleneSubmissionsBlocks = blocks,
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Sanierung-KI] Marktreferenz nicht ermittelbar: {ex.Message}");
        }

        // Hard-Constraint-Filter: Welche Verfahren sind technisch ueberhaupt zulaessig?
        // Bogen-Detection aus VSA-Codes (BCC = Bogen, mit Q1=Winkel)
        RulesFilterDto? rulesFilter = null;
        try
        {
            // Phase 5.1.B Etappe 3.E: via DI-Container.
            var rehabEngine = App.Resolve<Infrastructure.Sanierung.RehabilitationRulesEngine>();
            var allCodes = (record.VsaFindings ?? new List<Domain.Models.VsaFinding>())
                .Select(f => (Code: f.KanalSchadencode ?? "", Q1: f.Quantifizierung1))
                .ToList();

            // Bogen-Erkennung: BCC-Codes mit Q1=Winkel-Grad
            double maxBendDeg = 0;
            foreach (var (code, q1) in allCodes)
            {
                if (code.StartsWith("BCC", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(q1?.Replace(',', '.'),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out var deg))
                    {
                        if (deg > maxBendDeg) maxBendDeg = deg;
                    }
                }
            }

            var ctx = new Infrastructure.Sanierung.HaltungsKontext
            {
                DnMm = pipe.DiameterMm,
                Material = pipe.Material,
                LaengeM = pipe.LengthMeter,
                Nutzungsart = record.GetFieldValue("Nutzungsart"),
                Grundwasser = pipe.Groundwater,
                HasBendSevere = maxBendDeg >= 30,
                HasBendModerate = maxBendDeg >= 15 && maxBendDeg < 30,
                Zustandsklasse = int.TryParse(record.GetFieldValue("Zustandsklasse"), out var zkParsed) ? zkParsed : null,
            };

            var allCodesList = findings.Select(f => f.Code).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            var eval = rehabEngine.Evaluate(ctx, allCodesList);

            rulesFilter = new RulesFilterDto
            {
                EligibleProcedures = eval.Eligible.Select(e => e.Procedure.Name).ToList(),
                ConditionalProcedures = eval.Conditional.Select(e => e.Procedure.Name).ToList(),
                ExcludedProcedures = eval.Excluded
                    .Select(e => new ExcludedProcedure(e.Procedure.Id, e.Procedure.Name, e.Reason))
                    .ToList(),
                PromptHints = eval.PromptHints,
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Sanierung-KI] RulesFilter nicht ermittelbar: {ex.Message}");
        }

        return new SanierungOptimizationRequest
        {
            HaltungId = record.GetFieldValue("Haltungsname") ?? record.Id.ToString(),
            Findings  = findings,
            Pipe      = pipe,
            Rule      = ruleDto,
            MarktReferenz = marktRef,
            RulesFilter = rulesFilter,
        };
    }
}
