using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportExistingSampleSnapshotControllerTests
{
    [Fact]
    public async Task LoadAsync_laedt_samples_baut_ordinale_signaturen_und_loggt_counts()
    {
        var samples = new List<TrainingSample>
        {
            new() { SampleId = "1", Signature = "sig-a" },
            new() { SampleId = "2", Signature = "sig-a" },
            new() { SampleId = "3", Signature = "SIG-A" },
            new() { SampleId = "4", Signature = string.Empty },
            new() { SampleId = "5", Signature = null! }
        };
        var logLines = new List<string>();

        var snapshot = await TrainingBatchImportExistingSampleSnapshotController.LoadAsync(
            () => Task.FromResult(samples),
            logLines.Add);

        Assert.Same(samples, snapshot.AllSamples);
        Assert.Equal(
            new[] { "SIG-A", "sig-a" },
            snapshot.ExistingSignatures.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("Bestehende Samples: 5 (2 Signaturen)", Assert.Single(logLines));
    }
}
