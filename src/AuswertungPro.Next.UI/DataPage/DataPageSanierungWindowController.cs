using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageSanierungWindowRequest(
    HaltungRecord Record,
    string Holding,
    InitialFocusMode Focus,
    IReadOnlyList<string> RecommendedTemplates,
    AiRuntimeSettings? RuntimeSettings,
    RuleRecommendationDto? RuleRecommendation,
    Action<HoldingCost> ApplyCosts,
    Action OnOptimizationTransferred);

public sealed class DataPageSanierungWindowController
{
    private readonly IDialogService _dialogs;
    private readonly Func<HaltungRecord?> _getSelected;
    private readonly Func<string?, IReadOnlyList<string>> _parseRecommendedTemplates;
    private readonly Func<AiRuntimeSettings> _loadRuntimeSettings;
    private readonly Func<HaltungRecord, int, MeasureRecommendationResult> _recommendMeasures;
    private readonly Action<HaltungRecord, HoldingCost> _applyCostsToRecord;
    private readonly Action _markProjectDirty;
    private readonly Action<HaltungRecord> _refreshRecordInGrid;
    private readonly Action _scheduleAutoSave;
    private readonly Action<string> _setStatus;
    private readonly Action<DataPageSanierungWindowRequest> _showWindow;

    public DataPageSanierungWindowController(
        IDialogService dialogs,
        Func<HaltungRecord?> getSelected,
        Func<string?, IReadOnlyList<string>> parseRecommendedTemplates,
        Func<AiRuntimeSettings> loadRuntimeSettings,
        Func<HaltungRecord, int, MeasureRecommendationResult> recommendMeasures,
        Action<HaltungRecord, HoldingCost> applyCostsToRecord,
        Action markProjectDirty,
        Action<HaltungRecord> refreshRecordInGrid,
        Action scheduleAutoSave,
        Action<string> setStatus,
        Action<DataPageSanierungWindowRequest> showWindow)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _getSelected = getSelected ?? throw new ArgumentNullException(nameof(getSelected));
        _parseRecommendedTemplates = parseRecommendedTemplates ?? throw new ArgumentNullException(nameof(parseRecommendedTemplates));
        _loadRuntimeSettings = loadRuntimeSettings ?? throw new ArgumentNullException(nameof(loadRuntimeSettings));
        _recommendMeasures = recommendMeasures ?? throw new ArgumentNullException(nameof(recommendMeasures));
        _applyCostsToRecord = applyCostsToRecord ?? throw new ArgumentNullException(nameof(applyCostsToRecord));
        _markProjectDirty = markProjectDirty ?? throw new ArgumentNullException(nameof(markProjectDirty));
        _refreshRecordInGrid = refreshRecordInGrid ?? throw new ArgumentNullException(nameof(refreshRecordInGrid));
        _scheduleAutoSave = scheduleAutoSave ?? throw new ArgumentNullException(nameof(scheduleAutoSave));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _showWindow = showWindow ?? throw new ArgumentNullException(nameof(showWindow));
    }

    public void Open(HaltungRecord? record, InitialFocusMode focus)
    {
        record ??= _getSelected();
        if (record is null)
            return;

        var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holding))
        {
            _dialogs.Warn("Haltungsname fehlt in der Zeile.", "Sanierungsmassnahmen");
            return;
        }

        var recommended = _parseRecommendedTemplates(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
        var cfg = _loadRuntimeSettings();
        RuleRecommendationDto? ruleDto = null;
        AiRuntimeSettings? runtimeSettings = null;

        if (cfg.Enabled)
        {
            runtimeSettings = cfg;
            var ruleResult = _recommendMeasures(record, 5);
            if (ruleResult.Measures.Count > 0)
            {
                ruleDto = new RuleRecommendationDto
                {
                    Measures = ruleResult.Measures,
                    EstimatedCost = ruleResult.EstimatedTotalCost,
                    UsedTrainedModel = ruleResult.UsedTrainedModel
                };
            }
        }

        _showWindow(new DataPageSanierungWindowRequest(
            record,
            holding,
            focus,
            recommended,
            runtimeSettings,
            ruleDto,
            cost => _applyCostsToRecord(record, cost),
            () =>
            {
                _markProjectDirty();
                _refreshRecordInGrid(record);
                _scheduleAutoSave();
                _setStatus($"KI-Sanierungsvorschlag übertragen: {holding}");
            }));
    }
}
