using System;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Evidence;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingTrainingFrameSaveResult(string? Path, string? Error);

public sealed class CodingTrainingFrameStore
{
    private readonly Func<string> _knowledgeRootProvider;
    private readonly Func<string, string, EvidenceFrameAnnotation, bool> _saveAnnotatedFrame;
    private readonly ITrainingFrameStore _goldFrameStore;
    private readonly Func<string, string?> _codeLabelLookup;

    public CodingTrainingFrameStore()
        : this(
            () => KnowledgeBasePaths.GetRoot(),
            new EvidenceFrameImageRenderer().SaveAnnotatedFrame,
            new TrainingFrameFileStore())
    {
    }

    public CodingTrainingFrameStore(ITrainingFrameStore goldFrameStore)
        : this(
            () => KnowledgeBasePaths.GetRoot(),
            new EvidenceFrameImageRenderer().SaveAnnotatedFrame,
            goldFrameStore)
    {
    }

    public CodingTrainingFrameStore(
        Func<string> knowledgeRootProvider,
        Func<string, string, EvidenceFrameAnnotation, bool>? saveAnnotatedFrame = null,
        ITrainingFrameStore? goldFrameStore = null,
        Func<string, string?>? codeLabelLookup = null)
    {
        _knowledgeRootProvider = knowledgeRootProvider ?? throw new ArgumentNullException(nameof(knowledgeRootProvider));
        _saveAnnotatedFrame = saveAnnotatedFrame ?? new EvidenceFrameImageRenderer().SaveAnnotatedFrame;
        _goldFrameStore = goldFrameStore ?? new TrainingFrameFileStore();
        _codeLabelLookup = codeLabelLookup ?? VsaCodeResolver.LookupLabel;
    }

    public Task<CodingTrainingFrameSaveResult> SaveGoldFrameAsync(
        CodingEvent codingEvent,
        byte[]? preferredFrameBytes,
        Func<Task<byte[]?>> captureFallback)
        => SaveGoldFrameAsync(
            codingEvent,
            sourceFramePath: null,
            preferredFrameBytes,
            captureFallback);

    public async Task<CodingTrainingFrameSaveResult> SaveGoldFrameAsync(
        CodingEvent codingEvent,
        string? sourceFramePath,
        byte[]? preferredFrameBytes,
        Func<Task<byte[]?>> captureFallback)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);
        ArgumentNullException.ThrowIfNull(captureFallback);

        try
        {
            var codeFolder = PersonalGoldMainCodeCatalog.FormatFolderName(
                codingEvent.Entry.Code,
                _codeLabelLookup);
            var dir = Path.Combine(
                _knowledgeRootProvider(),
                "gold_frames",
                codeFolder);
            if (!string.IsNullOrWhiteSpace(sourceFramePath))
            {
                var storedPath = await _goldFrameStore
                    .StoreExistingAsync(sourceFramePath, dir)
                    .ConfigureAwait(false);
                return !string.IsNullOrWhiteSpace(storedPath)
                    ? new CodingTrainingFrameSaveResult(storedPath, null)
                    : new CodingTrainingFrameSaveResult(
                        null,
                        "vorhandenes Bild konnte nicht uebernommen werden");
            }

            var bytes = preferredFrameBytes;
            if (bytes is null || bytes.Length == 0)
                bytes = await captureFallback().ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
                return new CodingTrainingFrameSaveResult(null, "kein Frame verfuegbar");

            var storedBytesPath = await _goldFrameStore
                .StoreBytesAsync(bytes, ".png", dir)
                .ConfigureAwait(false);
            return !string.IsNullOrWhiteSpace(storedBytesPath)
                ? new CodingTrainingFrameSaveResult(storedBytesPath, null)
                : new CodingTrainingFrameSaveResult(null, "Frame konnte nicht uebernommen werden");
        }
        catch (Exception ex)
        {
            return new CodingTrainingFrameSaveResult(null, ex.Message);
        }
    }

    public CodingTrainingFrameSaveResult SaveEvidenceFrame(CodingEvent codingEvent, string? rawFramePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawFramePath) || !File.Exists(rawFramePath))
                return new CodingTrainingFrameSaveResult(null, "kein Rohbild fuer Beweisbild verfuegbar");

            var dir = Path.Combine(_knowledgeRootProvider(), "gold_frames_annotated");
            var file = Path.Combine(dir, $"{codingEvent.EventId:N}_annotated.png");
            var saved = _saveAnnotatedFrame(
                rawFramePath,
                file,
                CodingEvidenceAnnotationBuilder.Build(codingEvent));

            return saved
                ? new CodingTrainingFrameSaveResult(file, null)
                : new CodingTrainingFrameSaveResult(null, "Beweisbild konnte nicht erstellt werden");
        }
        catch (Exception ex)
        {
            return new CodingTrainingFrameSaveResult(null, ex.Message);
        }
    }
}
