using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public class KnowledgeBaseManagerEligibilityTests
{
    public KnowledgeBaseManagerEligibilityTests()
    {
        // VsaCodeResolver ist statisch — Minimal-Katalog konfigurieren damit LookupLabel("BAB") != null.
        VsaCodeResolver.ConfigureCatalog(new MinimalCatalog());
    }

    private static TrainingSample BaseSample() => new()
    {
        SampleId = "s1", CaseId = "c1", Code = "BAB",
        Beschreibung = "Riss laengs, 12 Uhr, Scheitel",
        MeterStart = 3.0, MeterEnd = 3.0,
        InspectionDate = new DateTime(2024, 5, 1),
        TrainingEligible = true
    };

    [Fact]
    public void IndexWorthy_True_ForEligibleSample()
        => Assert.True(KnowledgeBaseManager.IsIndexWorthy(BaseSample()));

    [Fact]
    public void IndexWorthy_False_WhenInspectionDateMissing()
    {
        var s = BaseSample();
        s.InspectionDate = null;
        s.TrainingEligible = false;
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(s));
    }

    // Minimaler Inline-Katalog fuer diesen Test — nur "BAB" benoetigt.
    private sealed class MinimalCatalog : ICodeCatalogProvider
    {
        private static readonly CodeDefinition[] Codes =
        {
            new() { Code = "BAB", Title = "Risse", IsSelectable = true }
        };

        public IReadOnlyList<CodeDefinition> GetAll() => Codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = Codes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                  ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new InvalidOperationException("Test catalog is read-only.");

        public IReadOnlyList<string> AllowedCodes()
            => Codes.Select(c => c.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
