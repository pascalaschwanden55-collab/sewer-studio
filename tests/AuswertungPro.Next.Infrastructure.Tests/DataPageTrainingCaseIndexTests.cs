using AuswertungPro.Next.Application.DataPage;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class DataPageTrainingCaseIndexTests
{
    [Fact]
    public void ReplaceCaseIds_normalisiert_case_ids_und_verwirft_leere_werte()
    {
        var index = new TrainingCaseIndex();

        index.ReplaceCaseIds(new[]
        {
            " 20250602_06.24341-35625 ",
            "",
            "20260101_07.1028055-10.1064892",
            null
        });

        Assert.Equal(
            new[] { "06.24341-35625", "07.1028055-10.1064892" },
            index.TrainedHaltungen.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ReplaceCaseIds_ersetzt_bisherige_werte()
    {
        var index = new TrainingCaseIndex();
        index.ReplaceCaseIds(new[] { "20250602_06.24341-35625" });

        index.ReplaceCaseIds(new[] { "20260101_07.1028055-10.1064892" });

        Assert.Equal("07.1028055-10.1064892", Assert.Single(index.TrainedHaltungen));
    }

    [Theory]
    [InlineData("06.24341-35625")]
    [InlineData("24341-35625")]
    [InlineData("07.1028055-10.1064892")]
    [InlineData("1028055-1064892")]
    public void IsTrainedCase_erkennt_exakt_und_ohne_knoten_praefixe(string haltungsname)
    {
        var index = new TrainingCaseIndex();
        index.ReplaceCaseIds(new[]
        {
            "20250602_06.24341-35625",
            "20260101_07.1028055-10.1064892"
        });

        Assert.True(index.IsTrainedCase(haltungsname));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("01.111-02.222")]
    public void IsTrainedCase_gibt_false_fuer_leere_oder_unbekannte_haltungen(string? haltungsname)
    {
        var index = new TrainingCaseIndex();
        index.ReplaceCaseIds(new[] { "20250602_06.24341-35625" });

        Assert.False(index.IsTrainedCase(haltungsname));
    }
}
