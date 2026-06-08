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

            Assert.Contains("survivor", ids);
            for (int i = 0; i < n; i++)
            {
                Assert.Contains($"add-{i}", ids);          // kein Append verloren
                Assert.DoesNotContain($"del-{i}", ids);    // alle Deletes wirksam
            }
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
