using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class TrainingSamplesStoreEvalLeakageTests
{
    [Fact]
    public async Task SaveUndMerge_BlockierenV2HashUndV2Haltung()
    {
        var previousKnowledgeRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var root = Path.Combine(Path.GetTempPath(), "TrainingStoreEvalLeakage", Guid.NewGuid().ToString("N"));
        var knowledgeRoot = Path.Combine(root, "knowledge");
        var evalRoot = Path.Combine(root, "eval_set");
        var evalV2 = Path.Combine(evalRoot, "v2");
        var evalImage = Path.Combine(evalV2, "images", "eval.png");
        var cleanImage = Path.Combine(root, "clean.png");

        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", knowledgeRoot);
        KnowledgeBasePaths.InvalidateCache();
        try
        {
            Directory.CreateDirectory(Path.Combine(evalV2, "images"));
            Directory.CreateDirectory(Path.Combine(evalV2, "labels"));
            File.WriteAllBytes(evalImage, [1, 3, 5, 7]);
            File.WriteAllBytes(cleanImage, [2, 4, 6, 8]);
            File.WriteAllText(Path.Combine(evalV2, "_candidates.json"), """
                [
                  {
                    "id": "eval-v2",
                    "frame_path": "eval.png",
                    "haltung_key": "100-200",
                    "code_full": "BAB"
                  }
                ]
                """);
            File.WriteAllText(Path.Combine(evalV2, "_manifest.json"), "{\"frozen\":true}");
            EvalSetManifestHasher.ComputeAndStoreHashes(evalV2);
            TrainingSamplesStore.ConfigureEvalProtection(evalRoot);

            await TrainingSamplesStore.SaveAsync([
                Sample("eval-hash", "900-901", evalImage),
                Sample("eval-haltung", "100-200", cleanImage),
                Sample("clean", "300-400", cleanImage)
            ]);

            var afterSave = await TrainingSamplesStore.LoadAsync();
            Assert.Equal(["clean"], afterSave.Select(sample => sample.SampleId));

            // Simuliert einen Altbestand, der vor Einfuehrung der Sperre gespeichert wurde.
            TrainingSamplesStore.ConfigureEvalProtection(Path.Combine(root, "noch-nicht-konfiguriert"));
            await TrainingSamplesStore.SaveAsync([
                Sample("eval-altbestand", "900-901", evalImage),
                Sample("clean", "300-400", cleanImage)
            ]);
            TrainingSamplesStore.ConfigureEvalProtection(evalRoot);
            var protectedLoad = await TrainingSamplesStore.LoadAsync();
            Assert.Equal(["clean"], protectedLoad.Select(sample => sample.SampleId));

            await TrainingSamplesStore.MergeAndSaveAsync([
                Sample("eval-hash-merge", "901-902", evalImage),
                Sample("clean-merge", "301-401", cleanImage)
            ]);

            var afterMerge = await TrainingSamplesStore.LoadAsync();
            Assert.Equal(["clean", "clean-merge"], afterMerge.Select(sample => sample.SampleId));
            Assert.DoesNotContain(afterMerge, sample =>
                EvalContaminationGuard.IsEvalContaminated(
                    EvalContaminationGuard.LoadEvalImageHashes(evalRoot),
                    sample.FramePath));
        }
        finally
        {
            TrainingSamplesStore.ConfigureEvalProtection(null);
            Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", previousKnowledgeRoot);
            KnowledgeBasePaths.InvalidateCache();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static TrainingSample Sample(string id, string caseId, string framePath)
        => new()
        {
            SampleId = id,
            CaseId = caseId,
            Code = "BAB",
            Beschreibung = "Gepruefter Laengsriss",
            FramePath = framePath,
            Signature = id
        };
}
