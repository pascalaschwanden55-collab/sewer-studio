using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolReviewCandidateFilterTests
{
    private static TrainingSample S(string code, TrainingSampleStatus status = TrainingSampleStatus.New)
        => new() { SampleId = code, Code = code, Beschreibung = "x", Status = status,
                   InspectionDate = new DateTime(2023, 1, 1), TrainingEligible = true };

    private sealed class Catalog : ICodeCatalogProvider
    {
        private readonly Dictionary<string, CodeDefinition> _c = new()
        {
            ["BAB"] = new CodeDefinition { Code = "BAB", IsSelectable = true },
            ["BCCYY"] = new CodeDefinition { Code = "BCCYY", IsSelectable = false, IsObservedExtension = true },
        };
        public IReadOnlyList<CodeDefinition> GetAll() => _c.Values.ToList();
        public bool TryGet(string code, out CodeDefinition def) { var ok = _c.TryGetValue(code, out var d); def = d ?? new CodeDefinition(); return ok; }
        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new NotSupportedException();
        public IReadOnlyList<string> AllowedCodes() => _c.Values.Where(c => c.IsSelectable && !c.IsObservedExtension).Select(c => c.Code).ToList();
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => Array.Empty<string>();
    }

    [Fact]
    public void SelectCandidates_behaelt_nur_katalog_gueltige_New_Samples()
    {
        var samples = new[] { S("BAB"), S("MWST"), S("BCCYY"), S("BAB", TrainingSampleStatus.Approved) };

        var result = ProtocolReviewCandidateFilter.SelectCandidates(samples, new Catalog()).ToList();

        Assert.Single(result);
        Assert.Equal("BAB", result[0].Code);
        Assert.Equal(TrainingSampleStatus.New, result[0].Status);
    }
}
