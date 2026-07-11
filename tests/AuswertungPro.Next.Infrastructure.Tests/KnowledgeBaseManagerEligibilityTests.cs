using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public class KnowledgeBaseManagerEligibilityTests : IDisposable
{
    private readonly ICodeCatalogProvider? _previousCatalog;

    public KnowledgeBaseManagerEligibilityTests()
    {
        // Vorherigen Katalog sichern bevor wir den statischen Zustand aendern.
        _previousCatalog = VsaCodeResolver.CurrentCatalog;
        // VsaCodeResolver ist statisch — Minimal-Katalog konfigurieren damit LookupLabel("BAB") != null.
        VsaCodeResolver.ConfigureCatalog(new MinimalCatalog());
    }

    public void Dispose() => VsaCodeResolver.ConfigureCatalog(_previousCatalog);

    private static TrainingSample BaseSample() => new()
    {
        SampleId = "s1", CaseId = "c1", Code = "BAB",
        Beschreibung = "Riss laengs, 12 Uhr, Scheitel",
        MeterStart = 3.0, MeterEnd = 3.0,
        InspectionDate = new DateTime(2024, 5, 1),
        TrainingEligible = true,
        Status = TrainingSampleStatus.Approved,
        HumanConfirmed = true
    };

    [Fact]
    public void IndexWorthy_True_ForEligibleSample()
        => Assert.True(KnowledgeBaseManager.IsIndexWorthy(BaseSample()));

    [Fact]
    public void IndexWorthy_True_WhenInspectionDateMissing_RetrievalIsRecencyAgnostic()
    {
        // Entkopplung Retrieval <-> Training (2026-06-20): ein fachlich gueltiges Sample OHNE
        // Aufnahmedatum ist als Retrieval-Kontext weiterhin index-wuerdig. Die Recency-Schranke
        // gilt nur fuer den Trainingsexport, nicht fuer die KB.
        var s = BaseSample();
        s.InspectionDate = null;
        Assert.True(KnowledgeBaseManager.IsIndexWorthy(s));
    }

    [Fact]
    public void IndexWorthy_True_WhenTrainingNotEligible_RetrievalIsRecencyAgnostic()
    {
        // TrainingEligible=false (Trainings-Flag) darf die Retrieval-Indexierung NICHT mehr blockieren.
        var s = BaseSample();
        s.TrainingEligible = false;
        Assert.True(KnowledgeBaseManager.IsIndexWorthy(s));
    }

    [Theory]
    [InlineData(TrainingSampleStatus.New, null)]
    [InlineData(TrainingSampleStatus.Approved, null)]
    [InlineData(TrainingSampleStatus.Approved, false)]
    [InlineData(TrainingSampleStatus.Rejected, true)]
    public void IndexWorthy_False_WithoutConfirmedGold(
        TrainingSampleStatus status,
        bool? humanConfirmed)
    {
        var sample = BaseSample();
        sample.Status = status;
        sample.HumanConfirmed = humanConfirmed;

        Assert.False(KnowledgeBaseManager.IsIndexWorthy(sample));
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
