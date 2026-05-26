using System;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingSampleEligibilityTests
{
    [Theory]
    [InlineData("31.12.2021", false, TrainingSampleEligibility.LegacyBeforeCutoffReason)]
    [InlineData("01.01.2022", true, null)]
    [InlineData("2022-01-01", true, null)]
    [InlineData("Aufnahmen: 04.12.14 - 05.12.14", false, TrainingSampleEligibility.LegacyBeforeCutoffReason)]
    [InlineData("GEP Aufnahmen Altdorf 2025", true, null)]
    public void Evaluate_nutzt_2022_als_harten_Stichtag(string rawDate, bool expectedEligible, string? expectedReason)
    {
        var parsed = TrainingSampleEligibility.TryParseInspectionDate(rawDate);

        Assert.NotNull(parsed);
        var result = TrainingSampleEligibility.Evaluate(parsed);

        Assert.Equal(expectedEligible, result.IsEligible);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void Evaluate_sperrt_unbekanntes_Datum()
    {
        var result = TrainingSampleEligibility.Evaluate((DateTime?)null);

        Assert.False(result.IsEligible);
        Assert.Equal(TrainingSampleEligibility.MissingInspectionDateReason, result.Reason);
    }

    [Fact]
    public void Evaluate_mit_Katalog_sperrt_unbekannte_und_nicht_klickbare_Codes()
    {
        var catalog = new InMemoryCodeCatalogProvider(
        [
            new CodeDefinition { Code = "BBAA", IsSelectable = true },
            new CodeDefinition { Code = "BCCYY", IsSelectable = false, IsObservedExtension = true }
        ]);

        var valid = MakeSample("BBAA");
        var unknown = MakeSample("BZZZ");
        var observed = MakeSample("BCCYY");

        Assert.True(TrainingSampleEligibility.Evaluate(valid, catalog).IsEligible);

        var unknownResult = TrainingSampleEligibility.Evaluate(unknown, catalog);
        Assert.False(unknownResult.IsEligible);
        Assert.Equal(TrainingSampleEligibility.InvalidCatalogCodeReason, unknownResult.Reason);

        var observedResult = TrainingSampleEligibility.Evaluate(observed, catalog);
        Assert.False(observedResult.IsEligible);
        Assert.Equal(TrainingSampleEligibility.InvalidCatalogCodeReason, observedResult.Reason);
    }

    private static TrainingSample MakeSample(string code)
        => new()
        {
            Code = code,
            InspectionDate = new DateTime(2022, 1, 1),
            TrainingEligible = true
        };

    private sealed class InMemoryCodeCatalogProvider : ICodeCatalogProvider
    {
        private readonly IReadOnlyList<CodeDefinition> _codes;

        public InMemoryCodeCatalogProvider(IReadOnlyList<CodeDefinition> codes)
            => _codes = codes;

        public IReadOnlyList<CodeDefinition> GetAll()
            => _codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = _codes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new InvalidOperationException("Test catalog is read-only.");

        public IReadOnlyList<string> AllowedCodes()
            => _codes.Where(c => c.IsSelectable && !c.IsObservedExtension).Select(c => c.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
