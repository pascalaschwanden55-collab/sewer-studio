using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

public sealed class PersonalGoldAlbumServiceTests
{
    [Fact]
    public async Task LoadAsync_gruppiert_nur_persoenliche_Handlabels_nach_Hauptcode()
    {
        var fullBab = PersonalGold("gold-1", "Pascal", "BABAA", @"C:\gold\1.jpg");
        var incompleteBab = PersonalGold("gold-2", "Pascal", "BABBB", @"C:\gold\2.jpg");
        incompleteBab.SamMaskRle = null;
        var missingBca = PersonalGold("gold-3", "Pascal", "BCAAA", @"C:\gold\missing.jpg");
        var otherUser = PersonalGold("gold-4", "Andere Person", "BCCAA", @"C:\gold\4.jpg");
        var automatic = PersonalGold("gold-5", "Pascal", "BDAAA", @"C:\gold\5.jpg");
        automatic.SourceType = SourceTypeNames.BatchImport;
        var store = new FakeSampleStore(
            [fullBab, incompleteBab, missingBca, otherUser, automatic]);
        var service = new PersonalGoldAlbumService(
            store,
            path => !path.EndsWith("missing.jpg", StringComparison.OrdinalIgnoreCase));

        var result = await service.LoadAsync("Pascal");

        Assert.Equal(3, result.TotalSamples);
        Assert.Equal(2, result.FullGoldSamples);
        Assert.Equal(1, result.IncompleteSamples);
        Assert.Equal(1, result.MissingFiles);
        Assert.Equal(["BAB", "BCA"], result.Groups.Select(group => group.MainCode));

        var bab = Assert.Single(result.Groups, group => group.MainCode == "BAB");
        Assert.Equal(2, bab.Items.Count);
        Assert.Equal(1, bab.FullGoldCount);
        Assert.Equal("Segmentierung fehlt", bab.Items.Single(item => !item.IsFullGold).GeometryStatus);

        var bca = Assert.Single(result.Groups, group => group.MainCode == "BCA");
        Assert.False(Assert.Single(bca.Items).FileExists);
    }

    [Fact]
    public async Task LoadAsync_zeigt_bestaetigtes_Pdf_ohne_Maske_als_unvollstaendig()
    {
        var pdf = PersonalGold("pdf-1", "Pascal", "BABBC", @"C:\gold\pdf.jpg");
        pdf.SourceType = SourceTypeNames.PdfPhoto;
        pdf.SourceReferenceCode = "BABBC";
        pdf.SourceReferenceDescription = "Riss, komplexe Rissbildung";
        pdf.Notes =
            "PDF-Operateurreferenz: haltung.pdf; " +
            "SHA-256=8a7cfb71d1289694b8a650fe2c49357840fe1935ac120b8fb83d24f899c99c6f; " +
            "Seite=3; Foto=42.jpg; Zuordnung=photo_id";
        pdf.SamMaskRle = null;
        var service = new PersonalGoldAlbumService(
            new FakeSampleStore([pdf]),
            _ => true);

        var result = await service.LoadAsync("Pascal");

        var item = Assert.Single(Assert.Single(result.Groups).Items);
        Assert.False(item.IsFullGold);
        Assert.Equal("Segmentierung fehlt", item.GeometryStatus);
    }

    private static TrainingSample PersonalGold(
        string sampleId,
        string user,
        string code,
        string framePath)
        => new()
        {
            SampleId = sampleId,
            CaseId = "haltung-1",
            Code = code,
            Beschreibung = "Persoenlich geprueftes Beispiel",
            FramePath = framePath,
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.ManualCoding,
            MatchLevel = MatchLevelNames.ReviewApproved,
            HumanConfirmed = true,
            Corrected = false,
            ConfirmedByUser = user,
            ConfirmedAtUtc = new DateTime(2026, 7, 23, 8, 0, 0, DateTimeKind.Utc),
            BboxXCenter = 0.5,
            BboxYCenter = 0.5,
            BboxWidth = 0.2,
            BboxHeight = 0.2,
            SamMaskRle = "0,4050,1,3949",
            SamMaskImageWidth = 100,
            SamMaskImageHeight = 80
        };

    private sealed class FakeSampleStore(List<TrainingSample> samples) : ITrainingSampleStore
    {
        public Task<List<TrainingSample>> LoadAsync() => Task.FromResult(samples);
        public Task SaveAsync(List<TrainingSample> values) => throw new NotSupportedException();
        public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> values) => throw new NotSupportedException();
        public Task MergeAndSaveAsync(List<TrainingSample> values) => throw new NotSupportedException();
        public Task<bool> TryAddNewAsync(TrainingSample sample, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RemoveBySampleIdAsync(string sampleId) => throw new NotSupportedException();
        public Task<bool> ReplaceBySampleIdAsync(TrainingSample sample) => throw new NotSupportedException();
    }
}
