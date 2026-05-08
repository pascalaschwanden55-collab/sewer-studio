using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Annotation;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Slice 1 (Operateur-Annotation): Der Adapter um KnowledgeBaseManager
/// muss <c>IndexSampleAsync(...) -> Task&lt;bool&gt;</c> in den Vertrag
/// <c>Task IndexSampleAsync(...)</c> uebersetzen, wobei <c>false</c> als
/// Fehler erkannt wird (nicht stillschweigend geschluckt — sonst wuerde
/// CommitAsync KB als "indexed" markieren, obwohl es nicht stimmt).
/// </summary>
public sealed class KnowledgeBaseIndexerAdapterTests
{
    [Fact]
    public async Task IndexSampleAsync_HappyPath_ReturnsCompletedTask()
    {
        var calls = 0;
        var adapter = new KnowledgeBaseIndexerAdapter(
            (sample, ct) => { calls++; return Task.FromResult(true); });

        await adapter.IndexSampleAsync(NewSample(), CancellationToken.None);

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task IndexSampleAsync_FalseReturn_ThrowsKbIndexFailedException()
    {
        var adapter = new KnowledgeBaseIndexerAdapter(
            (sample, ct) => Task.FromResult(false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.IndexSampleAsync(NewSample(), CancellationToken.None));
    }

    [Fact]
    public async Task IndexSampleAsync_InnerException_BubblesUp()
    {
        var adapter = new KnowledgeBaseIndexerAdapter(
            (sample, ct) => throw new InvalidOperationException("ollama down"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.IndexSampleAsync(NewSample(), CancellationToken.None));

        Assert.Equal("ollama down", ex.Message);
    }

    [Fact]
    public async Task IndexSampleAsync_OperationCanceled_ThrowsOce()
    {
        var adapter = new KnowledgeBaseIndexerAdapter(
            (sample, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(true);
            });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.IndexSampleAsync(NewSample(), cts.Token));
    }

    private static TrainingSample NewSample() => new()
    {
        SampleId = "abc",
        CaseId = "c1",
        Code = "BAB B",
    };
}
