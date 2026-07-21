using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class TeacherAnnotationGalleryServiceTests
{
    [Fact]
    public void FilterCodes_sind_distinct_und_sortiert()
    {
        var codes = TeacherAnnotationGalleryService.BuildFilterCodes(
            [
                Make("a", "BDD"),
                Make("b", "BAB"),
                Make("c", "bdd"),
                Make("d", ""),
            ]);

        Assert.Equal(["BAB", "BDD"], codes);
    }

    [Fact]
    public void FilterByCode_filtert_case_insensitive_und_Alle_liefert_alle()
    {
        var annotations = new[] { Make("a", "BAB"), Make("b", "BDD") };

        Assert.Equal(2, TeacherAnnotationGalleryService.FilterByCode(annotations, "Alle").Count);
        Assert.Equal("b", Assert.Single(TeacherAnnotationGalleryService.FilterByCode(annotations, "bdd")).AnnotationId);
    }

    [Fact]
    public async Task LoadAsync_liefert_alle_Annotationen()
    {
        await WithTempKnowledgeRoot(async () =>
        {
            await TeacherAnnotationStore.AppendAsync(Make("first", "BAB"), Make("second", "BDD"));

            var snapshot = await new TeacherAnnotationGalleryService().LoadAsync();

            Assert.Equal(["first", "second"], snapshot.Annotations.Select(a => a.AnnotationId));
            Assert.Equal(["BAB", "BDD"], snapshot.FilterCodes);
        });
    }

    [Fact]
    public async Task DeleteAsync_entfernt_store_eintrag_und_neben_dateien()
    {
        await WithTempKnowledgeRoot(async () =>
        {
            var root = KnowledgeBasePaths.GetRoot();
            var frame = Path.Combine(root, "frame.png");
            var crop = Path.Combine(root, "crop.png");
            var label = Path.Combine(root, "label.txt");
            await File.WriteAllTextAsync(frame, "frame");
            await File.WriteAllTextAsync(crop, "crop");
            await File.WriteAllTextAsync(label, "label");

            await TeacherAnnotationStore.AppendAsync(new TeacherAnnotation
            {
                AnnotationId = "delete-me",
                VsaCode = "BAB",
                FullFramePath = frame,
                CroppedRegionPath = crop,
                YoloAnnotationPath = label,
            });

            await new TeacherAnnotationGalleryService().DeleteAsync((await TeacherAnnotationStore.LoadAsync()).Single());

            Assert.Empty(await TeacherAnnotationStore.LoadAsync());
            Assert.False(File.Exists(frame));
            Assert.False(File.Exists(crop));
            Assert.False(File.Exists(label));
        });
    }

    private static TeacherAnnotation Make(string id, string code) => new()
    {
        AnnotationId = id,
        VsaCode = code,
    };

    private static async Task WithTempKnowledgeRoot(Func<Task> body)
    {
        var previous = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var temp = Path.Combine(Path.GetTempPath(), "sewer-teacher-gallery-tests", Guid.NewGuid().ToString("N"));
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
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }
}
