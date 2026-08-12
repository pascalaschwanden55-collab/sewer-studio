using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class TeacherAnnotationStoreTests
{
    private static TeacherAnnotation Make(string id) => new() { AnnotationId = id, VsaCode = "BAB" };

    [Fact]
    public async Task DeleteAsync_RemovesById_AndReportsResult()
    {
        await WithTempKnowledgeRoot(async () =>
        {
            await TeacherAnnotationStore.AppendAsync(Make("a"), Make("b"));

            Assert.True(await TeacherAnnotationStore.DeleteAsync("a"));
            Assert.False(await TeacherAnnotationStore.DeleteAsync("a"));            // schon weg
            Assert.False(await TeacherAnnotationStore.DeleteAsync("does-not-exist"));

            var rest = await TeacherAnnotationStore.LoadAsync();
            Assert.Single(rest);
            Assert.Equal("b", rest[0].AnnotationId);
        });
    }

    [Fact]
    public async Task ConcurrentAppendAndDelete_LosesNoAnnotation()
    {
        // R2-Regression: Delete laeuft unter demselben _fileLock wie Append. Bei gleichzeitigem
        // Append+Delete darf KEINE frisch angehaengte Annotation verloren gehen und KEINE zu
        // loeschende wieder auftauchen (frueher: die UI schrieb am Lock vorbei -> Lost-Update).
        await WithTempKnowledgeRoot(async () =>
        {
            const int n = 25;
            await TeacherAnnotationStore.AppendAsync(
                Enumerable.Range(0, n).Select(i => Make($"del-{i}")).ToArray());
            await TeacherAnnotationStore.AppendAsync(Make("survivor"));

            var ops = new List<Task>();
            for (int i = 0; i < n; i++)
            {
                ops.Add(TeacherAnnotationStore.AppendAsync(Make($"add-{i}")));   // hinzufuegen
                ops.Add(TeacherAnnotationStore.DeleteAsync($"del-{i}"));         // gleichzeitig loeschen
            }
            await Task.WhenAll(ops);

            var ids = (await TeacherAnnotationStore.LoadAsync())
                .Select(a => a.AnnotationId).ToHashSet(StringComparer.Ordinal);

            var expected = Enumerable.Range(0, n)
                .Select(i => $"add-{i}")
                .Append("survivor")
                .ToHashSet(StringComparer.Ordinal);
            Assert.True(ids.SetEquals(expected), $"Unerwartete Annotationen: {string.Join(", ", ids.OrderBy(x => x))}");
        });
    }

    [Fact]
    public async Task Save_IsAtomic_AndKeepsBackup()
    {
        // R6: Speichern laeuft ueber temp -> File.Replace; kein temp-Rest, Vorgaenger als .bak.
        await WithTempKnowledgeRoot(async () =>
        {
            await TeacherAnnotationStore.AppendAsync(Make("v1"));
            await TeacherAnnotationStore.AppendAsync(Make("v2"));   // zweiter Save -> .bak vom ersten

            var store = Path.Combine(KnowledgeBasePaths.GetRoot(), "teacher_annotations.json");
            Assert.True(File.Exists(store));
            Assert.False(File.Exists(store + ".tmp"));   // kein halb geschriebener temp-Rest
            Assert.True(File.Exists(store + ".bak"));     // Vorgaenger-Stand gesichert
        });
    }

    [Theory]
    [InlineData("{ das ist kein gueltiges json ")]
    [InlineData("null")]
    public async Task AppendAsync_CorruptExistingFile_FailsClosedAndKeepsOriginal(string corruptJson)
    {
        // Unlesbar ist kein leerer Erstlauf: Die Mutation muss abbrechen und den
        // vorhandenen Inhalt samt forensischer Kopie unveraendert lassen.
        await WithTempKnowledgeRoot(async () =>
        {
            var store = Path.Combine(KnowledgeBasePaths.GetRoot(), "teacher_annotations.json");
            await File.WriteAllTextAsync(store, corruptJson);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => TeacherAnnotationStore.AppendAsync(Make("neu")));

            Assert.Contains("NICHT veraendert", error.Message, StringComparison.Ordinal);
            Assert.Equal(corruptJson, await File.ReadAllTextAsync(store));
            Assert.True(File.Exists(store + ".corrupt"));
        });
    }

    [Fact]
    public async Task FileStore_LockedExistingFile_FailsClosedAndKeepsOriginal()
    {
        await WithTempKnowledgeRoot(async () =>
        {
            var store = new TeacherAnnotationFileStore(KnowledgeBasePaths.GetRoot());
            await store.AppendAsync(Make("alt-1"), Make("alt-2"));
            var path = store.StoragePath;
            var original = await File.ReadAllBytesAsync(path);

            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => store.AppendAsync(Make("neu")));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => store.DeleteAsync("alt-1"));
            }

            Assert.Equal(original, await File.ReadAllBytesAsync(path));
        });
    }

    [Fact]
    public async Task FileStore_UsesExplicitRootAndKeepsParallelAppends()
    {
        await WithTempKnowledgeRoot(async () =>
        {
            var store = new TeacherAnnotationFileStore(KnowledgeBasePaths.GetRoot());
            await Task.WhenAll(Enumerable.Range(0, 12).Select(index =>
                store.AppendAsync(Make($"direct-{index}"))));

            var annotations = await store.LoadAsync();

            Assert.Equal(12, annotations.Count);
            Assert.Equal(12, annotations.Select(item => item.AnnotationId).Distinct().Count());
            Assert.StartsWith(KnowledgeBasePaths.GetRoot(), store.StoragePath, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static async Task WithTempKnowledgeRoot(Func<Task> body)
    {
        var previous = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var temp = Path.Combine(Path.GetTempPath(), "sewer-teacher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", temp);
        KnowledgeBasePaths.InvalidateCache();
        try
        {
            await body();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", previous);
            KnowledgeBasePaths.InvalidateCache();
            try { Directory.Delete(temp, recursive: true); } catch { /* best effort */ }
        }
    }
}
