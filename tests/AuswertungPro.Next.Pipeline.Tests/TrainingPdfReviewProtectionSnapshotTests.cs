using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingPdfReviewProtectionSnapshotTests
{
    [Fact]
    public void Constructor_normalisiert_und_kopiert_Schutzdaten_defensiv()
    {
        var hashes = new List<string>
        {
            " " + new string('A', 64) + " ",
        };
        var holdingKeys = new List<string>
        {
            "07.638910-1367",
        };

        var snapshot = new TrainingPdfReviewProtectionSnapshot(
            hashes,
            holdingKeys);
        hashes.Clear();
        holdingKeys.Clear();

        Assert.Contains(
            new string('a', 64),
            snapshot.ImageHashes);
        Assert.Contains("638910-1367", snapshot.HoldingKeys);
    }

    [Fact]
    public void Constructor_weist_ungueltigen_SHA256_zurueck()
    {
        var error = Assert.Throws<InvalidDataException>(
            () => new TrainingPdfReviewProtectionSnapshot(
                ["abc123"],
                ["100-200"]));

        Assert.Contains("SHA-256", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_weist_nicht_numerischen_Haltungskey_zurueck()
    {
        var error = Assert.Throws<InvalidDataException>(
            () => new TrainingPdfReviewProtectionSnapshot(
                [new string('a', 64)],
                ["keine-haltung"]));

        Assert.Contains("Haltungskennung", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
