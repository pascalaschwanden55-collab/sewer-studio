using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.LiveControl;

namespace AuswertungPro.Next.UI.DataPage;

public sealed class DataPageVideoAnalysisController : IDisposable
{
    private readonly object _httpClientsGate = new();
    private readonly Dictionary<TimeSpan, HttpClient> _httpClients = new();
    private readonly IDialogService _dialogs;
    private readonly Func<IReadOnlyList<HaltungRecord>> _getRecords;
    private readonly Func<HaltungRecord, string?> _ensureVideoPath;
    private readonly Func<IReadOnlyList<string>?> _getAllowedCodes;
    private readonly Func<AiRuntimeSettings> _loadRuntimeSettings;
    private readonly Func<IReadOnlySet<string>, IAiSuggestionPlausibilityService> _createPlausibility;
    private readonly Func<AiRuntimeSettings, IAiSuggestionPlausibilityService, HttpClient, IVideoAnalysisPipelineService> _createPipeline;
    private readonly Func<PipelineRequest, IVideoAnalysisPipelineService, PipelineResult?> _showPipelineWindow;
    private readonly Func<HaltungRecord, bool> _isSelected;
    private readonly Action<HaltungRecord> _markProjectDirty;
    private readonly Action<HaltungRecord> _refreshRecordInGrid;
    private readonly Action _refreshSelectedProtocolEntries;
    private readonly Action _scheduleAutoSave;
    private readonly Action<Action> _beginInvoke;
    private bool _disposed;

    public DataPageVideoAnalysisController(
        IDialogService dialogs,
        Func<IReadOnlyList<HaltungRecord>> getRecords,
        Func<HaltungRecord, string?> ensureVideoPath,
        Func<IReadOnlyList<string>?> getAllowedCodes,
        Func<AiRuntimeSettings> loadRuntimeSettings,
        Func<IReadOnlySet<string>, IAiSuggestionPlausibilityService> createPlausibility,
        Func<AiRuntimeSettings, IAiSuggestionPlausibilityService, HttpClient, IVideoAnalysisPipelineService> createPipeline,
        Func<PipelineRequest, IVideoAnalysisPipelineService, PipelineResult?> showPipelineWindow,
        Func<HaltungRecord, bool> isSelected,
        Action<HaltungRecord> markProjectDirty,
        Action<HaltungRecord> refreshRecordInGrid,
        Action refreshSelectedProtocolEntries,
        Action scheduleAutoSave,
        Action<Action> beginInvoke)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _getRecords = getRecords ?? throw new ArgumentNullException(nameof(getRecords));
        _ensureVideoPath = ensureVideoPath ?? throw new ArgumentNullException(nameof(ensureVideoPath));
        _getAllowedCodes = getAllowedCodes ?? throw new ArgumentNullException(nameof(getAllowedCodes));
        _loadRuntimeSettings = loadRuntimeSettings ?? throw new ArgumentNullException(nameof(loadRuntimeSettings));
        _createPlausibility = createPlausibility ?? throw new ArgumentNullException(nameof(createPlausibility));
        _createPipeline = createPipeline ?? throw new ArgumentNullException(nameof(createPipeline));
        _showPipelineWindow = showPipelineWindow ?? throw new ArgumentNullException(nameof(showPipelineWindow));
        _isSelected = isSelected ?? throw new ArgumentNullException(nameof(isSelected));
        _markProjectDirty = markProjectDirty ?? throw new ArgumentNullException(nameof(markProjectDirty));
        _refreshRecordInGrid = refreshRecordInGrid ?? throw new ArgumentNullException(nameof(refreshRecordInGrid));
        _refreshSelectedProtocolEntries = refreshSelectedProtocolEntries ?? throw new ArgumentNullException(nameof(refreshSelectedProtocolEntries));
        _scheduleAutoSave = scheduleAutoSave ?? throw new ArgumentNullException(nameof(scheduleAutoSave));
        _beginInvoke = beginInvoke ?? throw new ArgumentNullException(nameof(beginInvoke));
    }

    public void Open(HaltungRecord? record)
    {
        if (record is null)
            return;

        var videoPath = _ensureVideoPath(record);
        if (string.IsNullOrWhiteSpace(videoPath))
            return;

        var allowedCodes = _getAllowedCodes();
        if (allowedCodes is null || allowedCodes.Count == 0)
        {
            _dialogs.Warn("VSA-Code-Katalog ist leer oder nicht geladen.", "Videoanalyse KI");
            return;
        }

        var cfg = _loadRuntimeSettings();
        if (!cfg.Enabled)
        {
            _dialogs.Info("KI ist deaktiviert (SEWERSTUDIO_AI_ENABLED=0).", "Videoanalyse KI");
            return;
        }

        var timeout = cfg.OllamaRequestTimeout > TimeSpan.Zero
            ? cfg.OllamaRequestTimeout
            : TimeSpan.FromMinutes(30);
        var http = GetOrCreateHttpClient(timeout);
        var allowedSet = new HashSet<string>(allowedCodes, StringComparer.OrdinalIgnoreCase);
        var plausibility = _createPlausibility(allowedSet);
        var pipeline = _createPipeline(cfg, plausibility, http);
        var request = BuildRequest(record, videoPath, allowedCodes);
        var result = _showPipelineWindow(request, pipeline);

        if (result?.IsSuccess != true || result.Document is null)
            return;

        // Manuelle Eintraege bleiben geschuetzt; die alte Revision wird erst nach Bestaetigung archiviert.
        if (ProtocolReplacementService.HasManualCurrentEntries(record.Protocol)
            && !_dialogs.Confirm(
                "Diese Haltung enthaelt manuell codierte Eintraege.\n\n" +
                "Die KI-Reanalyse ersetzt das angezeigte Protokoll. Das bisherige Protokoll " +
                "wird in die Historie verschoben (wiederherstellbar).\n\nFortfahren?",
                "KI-Reanalyse"))
        {
            return;
        }

        record.Protocol = ProtocolReplacementService.PrepareReplacement(
            record.Protocol,
            result.Document,
            user: "KI-Reanalyse",
            archiveComment: "Auto-Archiv vor KI-Reanalyse");

        _markProjectDirty(record);
        _refreshRecordInGrid(record);
        if (_isSelected(record))
            _refreshSelectedProtocolEntries();

        _scheduleAutoSave();
    }

    public LiveControlRetryResult TryStartByName(string haltungsname)
    {
        if (string.IsNullOrWhiteSpace(haltungsname))
            return new LiveControlRetryResult(false, "Haltungsname fehlt.");

        var name = haltungsname.Trim();
        var record = _getRecords().FirstOrDefault(r =>
            string.Equals(r.GetFieldValue("Haltungsname"), name, StringComparison.OrdinalIgnoreCase));

        if (record is null)
            return new LiveControlRetryResult(
                false,
                $"Haltung '{name}' nicht im geladenen Projekt gefunden.");

        _beginInvoke(() => Open(record));

        return new LiveControlRetryResult(
            true,
            $"KI-Videoanalyse fuer '{name}' gestartet.");
    }

    public void Dispose()
    {
        lock (_httpClientsGate)
        {
            if (_disposed)
                return;

            _disposed = true;
            foreach (var httpClient in _httpClients.Values)
                httpClient.Dispose();
            _httpClients.Clear();
        }
    }

    private HttpClient GetOrCreateHttpClient(TimeSpan timeout)
    {
        lock (_httpClientsGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_httpClients.TryGetValue(timeout, out var existing))
                return existing;

            var created = new HttpClient { Timeout = timeout };
            _httpClients.Add(timeout, created);
            return created;
        }
    }

    private static PipelineRequest BuildRequest(
        HaltungRecord record,
        string videoPath,
        IReadOnlyList<string> allowedCodes)
    {
        var haltungId = record.GetFieldValue("Haltungsname") ?? record.Id.ToString();
        var reachLengthM = PipelineReachLengthParser.TryParse(record.GetFieldValue("Haltungslaenge_m"));
        return new PipelineRequest(haltungId, videoPath, allowedCodes, ReachLengthM: reachLengthM);
    }
}
