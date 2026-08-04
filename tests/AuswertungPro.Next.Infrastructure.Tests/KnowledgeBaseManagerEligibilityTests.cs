using System;
using System.Collections.Generic;
using System.IO;
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
        FramePath = typeof(KnowledgeBaseManagerEligibilityTests).Assembly.Location,
        TrainingEligible = true,
        Status = TrainingSampleStatus.Approved,
        HumanConfirmed = true,
        Corrected = false,
        ConfirmedByUser = Environment.UserName,
        ConfirmedAtUtc = new DateTime(2026, 7, 25, 8, 0, 0, DateTimeKind.Utc),
        SourceType = SourceTypeNames.ManualCoding,
        MatchLevel = MatchLevelNames.ReviewApproved,
        // Gold-Wahrheits-Haertung: IsIndexWorthy verlangt Box + SAM-Maske.
        BboxXCenter = 0.5, BboxYCenter = 0.5, BboxWidth = 0.2, BboxHeight = 0.2,
        SamMaskRle = "0,4050,1,3949", SamMaskImageWidth = 100, SamMaskImageHeight = 80
    };

    [Fact]
    public void IndexWorthy_True_ForEligibleSample()
    {
        var sample = BaseSample();

        Assert.True(ManualGoldTrainingPolicy.IsManuallyConfirmed(sample, Environment.UserName));
        Assert.True(ManualGoldTrainingPolicy.HasValidGoldBox(sample));
        Assert.True(ManualGoldTrainingPolicy.HasValidGoldSegmentation(sample));
        Assert.True(GoldDescriptionPolicy.IsKnowledgeTextReady(sample.Beschreibung));
        Assert.NotNull(VsaCodeResolver.LookupLabel(sample.Code));
        Assert.True(
            TrainingSamplePlausibility.IsFachlichPlausibel(sample, out var reason),
            reason);
        Assert.True(KnowledgeBaseManager.IsIndexWorthy(sample));
    }

    [Fact]
    public void IndexWorthy_True_ForPersonallyConfirmedPdfPhotoWithStrictProvenance()
    {
        var sample = BaseSample();
        sample.SourceType = SourceTypeNames.PdfPhoto;
        sample.SourceReferenceCode = "BAB";
        sample.SourceReferenceDescription = "Riss laengs, 12 Uhr, Scheitel";
        sample.Notes =
            "PDF-Operateurreferenz: 20231123_06.887943-90327.pdf; " +
            "SHA-256=8a7cfb71d1289694b8a650fe2c49357840fe1935ac120b8fb83d24f899c99c6f; " +
            "Seite=3; Foto=231123_115548_266.jpg; Zuordnung=time_meter_text";

        Assert.True(KnowledgeBaseManager.IsIndexWorthy(sample));
    }

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

    [Fact]
    public void IndexWorthy_False_ForDraft_EvenWithMaskAndBox()
    {
        // Entwuerfe (Status=Draft) duerfen nie in die KB — unabhaengig von Maske/Box.
        var sample = BaseSample();
        sample.Status = TrainingSampleStatus.Draft;

        Assert.False(KnowledgeBaseManager.IsIndexWorthy(sample));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("corrected")]
    [InlineData("confirmed-at")]
    [InlineData("match-level")]
    public void IndexWorthy_False_WhenManualGoldPolicyRequirementIsMissing(string field)
    {
        var sample = BaseSample();
        switch (field)
        {
            case "source":
                sample.SourceType = SourceTypeNames.BatchImport;
                break;
            case "corrected":
                sample.Corrected = null;
                break;
            case "confirmed-at":
                sample.ConfirmedAtUtc = null;
                break;
            case "match-level":
                sample.MatchLevel = MatchLevelNames.ExactMatch;
                break;
        }

        Assert.False(KnowledgeBaseManager.IsIndexWorthy(sample));
    }

    [Fact]
    public void IndexWorthy_False_WhenConfirmedByAnotherUser()
    {
        var sample = BaseSample();
        sample.ConfirmedByUser = Environment.UserName + "-andere-person";

        Assert.False(KnowledgeBaseManager.IsIndexWorthy(sample));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IndexWorthy_False_WithoutFramePath(string? framePath)
    {
        var sample = BaseSample();
        sample.FramePath = framePath ?? string.Empty;

        Assert.False(KnowledgeBaseManager.IsIndexWorthy(sample));
    }

    [Fact]
    public void IndexWorthy_False_WhenFrameFileIsMissing()
    {
        var sample = BaseSample();
        sample.FramePath = Path.Combine(
            Path.GetTempPath(),
            $"sewerstudio-missing-{Guid.NewGuid():N}.jpg");

        Assert.False(File.Exists(sample.FramePath));
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(sample));
    }

    [Fact]
    public void IndexWorthy_False_ForApproved_WithoutMaskOrBox()
    {
        // Gehaertete Gold-Wahrheit (defense-in-depth, schuetzt auch Alt-Entwuerfe):
        // Approved + HumanConfirmed reicht nicht mehr — ohne Box/Maske kein KB-Eintrag.
        var sample = BaseSample();
        sample.SamMaskRle = null;
        sample.SamMaskImageWidth = null;
        sample.SamMaskImageHeight = null;

        Assert.False(KnowledgeBaseManager.IsIndexWorthy(sample));

        sample = BaseSample();
        sample.BboxXCenter = null;

        Assert.False(KnowledgeBaseManager.IsIndexWorthy(sample));
    }

    [Fact]
    public void IndexWorthy_False_ForApproved_WithMalformedMask()
    {
        var sample = BaseSample();
        sample.SamMaskRle = "0,10,5";

        Assert.False(KnowledgeBaseManager.IsIndexWorthy(sample));
    }

    [Fact]
    public void IndexWorthy_False_ForInventedSubcodeWithKnownMainCode()
    {
        var sample = BaseSample();
        sample.Code = "BABZZ";

        Assert.NotNull(VsaCodeResolver.LookupLabel(sample.Code));
        Assert.False(VsaCodeResolver.IsExactSelectableCode(sample.Code));
        Assert.False(KnowledgeBaseManager.IsIndexWorthy(sample));
    }

    [Fact]
    public void ExactSelectableCode_RejectsObservedOrNonSelectableCatalogEntries()
    {
        VsaCodeResolver.ConfigureCatalog(new SelectionCatalog());

        Assert.True(VsaCodeResolver.IsExactSelectableCode("BAB"));
        Assert.False(VsaCodeResolver.IsExactSelectableCode("BABXA"));
        Assert.False(VsaCodeResolver.IsExactSelectableCode("BABXB"));
    }

    [Theory]
    [InlineData("Riss laengs — Lage und Ausmass ergaenzen")]
    [InlineData("Riss laengs — Lage und Ausmass ergänzen")]
    [InlineData("Riss laengs — Lage und Ausmaß ergänzen")]
    public void IndexWorthy_False_ForPlaceholderDescription(string description)
    {
        var sample = BaseSample();
        sample.Beschreibung = description;

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

    private sealed class SelectionCatalog : ICodeCatalogProvider
    {
        private static readonly CodeDefinition[] Codes =
        {
            new() { Code = "BAB", Title = "Risse", IsSelectable = true },
            new()
            {
                Code = "BABXA",
                Title = "Beobachtete Erweiterung",
                IsSelectable = true,
                IsObservedExtension = true,
            },
            new() { Code = "BABXB", Title = "Nur Ueberschrift", IsSelectable = false },
        };

        public IReadOnlyList<CodeDefinition> GetAll() => Codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = Codes.FirstOrDefault(candidate => string.Equals(
                      candidate.Code,
                      code,
                      StringComparison.OrdinalIgnoreCase))
                  ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new InvalidOperationException("Test catalog is read-only.");

        public IReadOnlyList<string> AllowedCodes()
            => Codes.Where(code => code.IsSelectable && !code.IsObservedExtension)
                .Select(code => code.Code)
                .ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
