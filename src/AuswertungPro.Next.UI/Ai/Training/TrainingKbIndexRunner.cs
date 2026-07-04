using System.Net.Http;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

public interface ITrainingKbIndexSession : IDisposable
{
    bool IsIndexed(string sampleId);

    bool IsPermanentlySkipped(TrainingSample sample);

    Task<bool> IndexSampleAsync(TrainingSample sample, CancellationToken ct);

    void CreateVersion(string notes);
}

public sealed class TrainingKbIndexRunner
{
    private readonly Func<CancellationToken, Task<bool>> _isOllamaReachableAsync;
    private readonly Func<ITrainingKbIndexSession> _createSession;
    private readonly Action<string> _log;
    private readonly string _unreachableMessage;
    private readonly Func<DateTime> _now;

    public TrainingKbIndexRunner(
        Func<CancellationToken, Task<bool>> isOllamaReachableAsync,
        Func<ITrainingKbIndexSession> createSession,
        Action<string> log,
        string unreachableMessage,
        Func<DateTime>? now = null)
    {
        _isOllamaReachableAsync = isOllamaReachableAsync ?? throw new ArgumentNullException(nameof(isOllamaReachableAsync));
        _createSession = createSession ?? throw new ArgumentNullException(nameof(createSession));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _unreachableMessage = unreachableMessage ?? throw new ArgumentNullException(nameof(unreachableMessage));
        _now = now ?? (() => DateTime.Now);
    }

    public static TrainingKbIndexRunner CreateDefault(
        OllamaConfig ollamaConfig,
        HttpClient httpClient,
        AppSettings? settings,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(ollamaConfig);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(log);

        return new TrainingKbIndexRunner(
            ct => TrainingOllamaReachabilityChecker.CheckAsync(ollamaConfig, ct),
            () => TrainingKbIndexSession.Create(httpClient, ollamaConfig, EvalContaminationSetProvider.Load(settings)),
            log,
            $"KB-Update uebersprungen: Ollama nicht erreichbar auf {ollamaConfig.BaseUri}");
    }

    public async Task<KbIndexOutcome> RunAsync(
        IReadOnlyList<TrainingSample> samples,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var indexedIds = new List<string>();
        var skippedIds = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            if (!await _isOllamaReachableAsync(ct))
            {
                _log(_unreachableMessage);
                return new KbIndexOutcome(indexedIds, skippedIds);
            }

            using var session = _createSession();
            var newlyIndexed = 0;
            foreach (var sample in samples)
            {
                ct.ThrowIfCancellationRequested();
                if (session.IsIndexed(sample.SampleId))
                {
                    indexedIds.Add(sample.SampleId);
                    continue;
                }

                if (session.IsPermanentlySkipped(sample))
                {
                    skippedIds.Add(sample.SampleId);
                    continue;
                }

                if (await session.IndexSampleAsync(sample, ct))
                {
                    indexedIds.Add(sample.SampleId);
                    newlyIndexed++;
                }
            }

            if (newlyIndexed > 0)
                session.CreateVersion($"Inkrementell {_now():yyyy-MM-dd HH:mm}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"KB-Update Fehler: {ex.Message}");
        }

        return new KbIndexOutcome(indexedIds, skippedIds);
    }
}

public sealed class TrainingKbIndexSession : ITrainingKbIndexSession
{
    private readonly KnowledgeBaseContext _context;
    private readonly KnowledgeBaseManager _manager;

    private TrainingKbIndexSession(
        KnowledgeBaseContext context,
        KnowledgeBaseManager manager)
    {
        _context = context;
        _manager = manager;
    }

    public static TrainingKbIndexSession Create(
        HttpClient httpClient,
        OllamaConfig ollamaConfig,
        EvalContaminationSets evalSets)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(ollamaConfig);
        ArgumentNullException.ThrowIfNull(evalSets);

        var context = new KnowledgeBaseContext();
        var embedder = new EmbeddingService(httpClient, ollamaConfig);
        var manager = new KnowledgeBaseManager(
            context,
            embedder,
            evalSets.ImageHashes,
            evalSets.HaltungKeys);
        return new TrainingKbIndexSession(context, manager);
    }

    public bool IsIndexed(string sampleId)
        => _manager.IsIndexed(sampleId);

    public bool IsPermanentlySkipped(TrainingSample sample)
        => _manager.IsPermanentlySkipped(sample);

    public Task<bool> IndexSampleAsync(TrainingSample sample, CancellationToken ct)
        => _manager.IndexSampleAsync(sample, ct);

    public void CreateVersion(string notes)
        => _manager.CreateVersion(notes);

    public void Dispose()
        => _context.Dispose();
}
