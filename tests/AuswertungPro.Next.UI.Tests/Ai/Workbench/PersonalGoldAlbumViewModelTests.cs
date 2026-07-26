using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PersonalGoldAlbumViewModelTests
{
    [Fact]
    public async Task Load_zeigt_alle_Beispiele_und_filtert_codeweise()
    {
        var babItems = new[]
        {
            Item("gold-1", "BAB", "BABAA"),
            Item("gold-2", "BAB", "BABBB", hasSegmentation: false)
        };
        var bcaItems = new[] { Item("gold-3", "BCA", "BCAAA") };
        var snapshot = new PersonalGoldAlbumSnapshot(
            [
                new PersonalGoldAlbumGroup("BAB", 1, babItems),
                new PersonalGoldAlbumGroup("BCA", 1, bcaItems)
            ],
            TotalSamples: 3,
            FullGoldSamples: 2,
            IncompleteSamples: 1,
            MissingFiles: 0);
        var viewModel = new PersonalGoldAlbumViewModel(
            new FakeAlbumService(snapshot),
            "Pascal");

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.CodeFilters.Count);
        Assert.Equal(3, viewModel.VisibleItems.Count);
        Assert.Equal("gold-1", viewModel.SelectedItem?.SampleId);
        Assert.Contains("3 persönliche Beispiele", viewModel.SummaryText);

        viewModel.SelectedCodeFilter = viewModel.CodeFilters.Single(
            filter => filter.MainCode == "BCA");

        var selected = Assert.Single(viewModel.VisibleItems);
        Assert.Equal("gold-3", selected.SampleId);
    }

    private static PersonalGoldAlbumItem Item(
        string sampleId,
        string mainCode,
        string code,
        bool hasSegmentation = true)
        => new(
            sampleId,
            mainCode,
            code,
            "Persoenlich geprueftes Beispiel",
            $@"C:\gold\{sampleId}.jpg",
            new DateTime(2026, 7, 23, 8, 0, 0, DateTimeKind.Utc),
            HasBbox: true,
            HasSegmentation: hasSegmentation,
            FileExists: true);

    private sealed class FakeAlbumService(PersonalGoldAlbumSnapshot snapshot)
        : IPersonalGoldAlbumService
    {
        public Task<PersonalGoldAlbumSnapshot> LoadAsync(
            string confirmedByUser,
            CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);
    }
}
