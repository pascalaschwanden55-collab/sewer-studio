using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportSampleRegistrar
{
    public static void RegisterAsReviewCandidates(
        IEnumerable<TrainingSample> samples,
        ISet<string> existingSignatures)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(existingSignatures);

        foreach (var sample in samples)
        {
            sample.Status = TrainingSampleStatus.New;
            existingSignatures.Add(sample.Signature);
        }
    }
}
