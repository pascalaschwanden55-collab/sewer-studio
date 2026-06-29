using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportSampleRegistrarTests
{
    [Fact]
    public void RegisterAsReviewCandidates_forces_new_status_and_registers_signatures()
    {
        var existingSignatures = new HashSet<string>(StringComparer.Ordinal) { "old" };
        var samples = new[]
        {
            new TrainingSample
            {
                Status = TrainingSampleStatus.Approved,
                Signature = "sig-a"
            },
            new TrainingSample
            {
                Status = TrainingSampleStatus.Rejected,
                Signature = "sig-b"
            }
        };

        TrainingBatchImportSampleRegistrar.RegisterAsReviewCandidates(samples, existingSignatures);

        Assert.All(samples, sample => Assert.Equal(TrainingSampleStatus.New, sample.Status));
        Assert.Equal(
            new[] { "old", "sig-a", "sig-b" },
            existingSignatures.Order(StringComparer.Ordinal).ToArray());
    }
}
