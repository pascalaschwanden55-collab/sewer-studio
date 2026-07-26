using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.UI.Tests.Ai.Workbench;

/// <summary>
/// Sichert die feine Anschluss-Bauart-Anreicherung am Pruefplatz-Service: mit Classifier kommen
/// Kandidaten mit Quelle "bca", ohne Classifier bleibt es leer (Knopf wirkungslos, kein Fehler).
/// SuggestBcaBauartAsync nutzt nur readFileBytes + Classifier — die uebrigen Abhaengigkeiten
/// duerfen im Test null sein.
/// </summary>
public sealed class AnnotationWorkbenchBcaBauartTests
{
    private static WorkbenchItem Item() => new("frame.png", "case1", 0, 0, null, null, null);

    private static AnnotationWorkbenchService Create(IBcaFineCodeClassifier? bcaClassifier, byte[] frameBytes)
        => new(
            samService: null!,
            pipelineClient: null!,
            retrieval: null,
            sampleStore: null!,
            frameStore: null!,
            resolveGoldFramesDir: null!,
            kbIndexer: null!,
            teacherStore: null!,
            teacherClassMap: null!,
            readFileBytes: _ => frameBytes,
            resolveEvalSetRoot: () => null,
            exportServiceFactory: null,
            isCodeKnown: null,
            bcaClassifier: bcaClassifier);

    [Fact]
    public async Task Ohne_Classifier_liefert_leere_Bauart_Kandidaten()
    {
        var sut = Create(bcaClassifier: null, frameBytes: [1, 2, 3]);

        var result = await sut.SuggestBcaBauartAsync(Item());

        Assert.Empty(result.Candidates);
        Assert.True(result.FrameUsable);
    }

    [Fact]
    public async Task Mit_Classifier_liefert_Bauart_Kandidaten_mit_Quelle_bca()
    {
        var classifier = new FakeClassifier(new BcaFineCodeSuggestion(
            new[] { new BcaFineCodeCandidate("BCAAA", 0.8) }, IsUncertain: false));
        var sut = Create(classifier, frameBytes: [1, 2, 3]);

        var result = await sut.SuggestBcaBauartAsync(Item());

        Assert.Single(result.Candidates);
        Assert.Equal("BCAAA", result.Candidates[0].VsaCode);
        Assert.Equal("bca", result.Candidates[0].Quelle);
    }

    private sealed class FakeClassifier(BcaFineCodeSuggestion answer) : IBcaFineCodeClassifier
    {
        public Task<BcaFineCodeSuggestion> SuggestAsync(string anschlussBildBase64, CancellationToken ct = default)
            => Task.FromResult(answer);
    }
}
