using System;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingTrainingFrameSaveResult(string? Path, string? Error);

public sealed class CodingTrainingFrameStore
{
    private readonly Func<string> _knowledgeRootProvider;
    private readonly Func<string, string, EvidenceFrameAnnotation, bool> _saveAnnotatedFrame;

    public CodingTrainingFrameStore()
        : this(() => KnowledgeBasePaths.GetRoot(), EvidenceFrameRenderer.SaveAnnotatedFrame)
    {
    }

    public CodingTrainingFrameStore(
        Func<string> knowledgeRootProvider,
        Func<string, string, EvidenceFrameAnnotation, bool>? saveAnnotatedFrame = null)
    {
        _knowledgeRootProvider = knowledgeRootProvider ?? throw new ArgumentNullException(nameof(knowledgeRootProvider));
        _saveAnnotatedFrame = saveAnnotatedFrame ?? EvidenceFrameRenderer.SaveAnnotatedFrame;
    }

    public async Task<CodingTrainingFrameSaveResult> SaveGoldFrameAsync(
        CodingEvent codingEvent,
        byte[]? preferredFrameBytes,
        Func<Task<byte[]?>> captureFallback)
    {
        try
        {
            var bytes = preferredFrameBytes;
            if (bytes is null || bytes.Length == 0)
                bytes = await captureFallback();
            if (bytes is null || bytes.Length == 0)
                return new CodingTrainingFrameSaveResult(null, "kein Frame verfügbar");

            var dir = Path.Combine(_knowledgeRootProvider(), "gold_frames");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{codingEvent.EventId:N}.png");
            await File.WriteAllBytesAsync(file, bytes);
            return new CodingTrainingFrameSaveResult(file, null);
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
                return new CodingTrainingFrameSaveResult(null, "kein Rohbild für Beweisbild verfügbar");

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
