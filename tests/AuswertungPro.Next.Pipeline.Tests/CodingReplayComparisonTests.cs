using AuswertungPro.Next.Application.UseCases.CodingReplay;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingReplayComparisonTests
{
    [Theory]
    [InlineData("BAJA", "BAJA,BCC", CodingReplayComparison.ExactCodePresent)]
    [InlineData("BAJA", "BAJ", CodingReplayComparison.SameFamilyPresent)]
    [InlineData("BAJA", "BCC", CodingReplayComparison.ReferenceNotProposed)]
    [InlineData("BAJA", "", CodingReplayComparison.ReferenceNotProposed)]
    [InlineData("LEER", "BCC", CodingReplayComparison.NoDamageCodeProposed)]
    [InlineData("LEER", "BBA", CodingReplayComparison.DamageCodeOnNegative)]
    public void Sichtbare_Codes_werden_ohne_erfundene_Fehlalarmbewertung_verglichen(
        string expected, string codes, CodingReplayComparison comparison)
    {
        var result = Result("measured", "EventsAdded", codes);
        Assert.Equal(comparison, CodingReplayComparer.Compare(expected, result));
    }

    [Theory]
    [InlineData("technical_error", "ReviewRequired", CodingReplayComparison.NotMeasured)]
    [InlineData("context_missing", "NotRun", CodingReplayComparison.NotMeasured)]
    [InlineData("measured", "ReviewRequired", CodingReplayComparison.ReviewRequired)]
    public void Leere_Fehlerantworten_zaehlen_nicht_als_bestaetigt_negativ(
        string status, string outcome, CodingReplayComparison comparison)
        => Assert.Equal(comparison, CodingReplayComparer.Compare("LEER", Result(status, outcome, "")));

    private static CodingReplayResult Result(string status, string outcome, string codes) =>
        new("test", status, new(outcome,
            codes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => new CodingReplayEvent(c, 1, null)).ToArray(),
            new Dictionary<string, string>()), null, 1);
}
